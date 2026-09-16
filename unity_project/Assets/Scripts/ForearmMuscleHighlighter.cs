using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Linq;
using System.IO;
using ChartAndGraph;

public class ForearmMuscleHighlighter : MonoBehaviour
{
    [Serializable]
    private class MuscleActivationData
    {
        public float[] emgarm; // Array containing EMG data received from the Python client
    }

    public enum SwingCondition
    {
        Forehand,
        Backhand
    }
    public SwingCondition currentCondition = SwingCondition.Forehand;

    public CanvasBarChart RealtimeMuscle;  // ✅ GraphChart 추가

    // Avatar Transform
    public Transform avatarTransform;
    public int udpPort;

    // Highlight material
    public Material highlightMaterial;

    // Muscle activation (values between 0.0 and 1.0)
    [Range(0f, 1f)] public float forearmFlexionMuscleActivation = 0.5f;
    [Range(0f, 1f)] public float forearmExtensionMuscleActivation = 0.5f;

    [HideInInspector]
    public Queue<(long, float[])> forearmQueue = new Queue<(long, float[])>();
    [HideInInspector]
    public (long, float[]) forearmChannels = (0L, new float[8]);  // 최신 값 (여전히 유지)

    // Transform references for muscle visualization
    private Transform rightForeArmFlexion;
    private Transform rightForeArmExtension;

    // UDP server setup
    private UdpClient udpClient;
    private Thread udpThread;
    private bool isRunning = false;
    private List<(long, float[])> timeStampedRecordingData = new List<(long, float[])>();

    // Butterworth Filters
    private ButterworthFilter[] bandpassFilters;
    private float forearmFlexionEMA;
    private float forearmExtensionEMA;

    // RMS buffers
    private int windowSize = 10;
    private List<float>[] rectifiedBuffers;

    // Threshold damping
    public float alpha = 0.2f;
    public float threshold = 0.05f;

    private List<float>[] recordingData; // 각 채널의 기록 데이터를 저장
    private bool isRecording = false; // 현재 기록 상태 플래그
    private float[] mvcValues; // 각 채널별 MVC 값을 저장
    public string mvcFilePath = "./RecordedData/"; // MVC 값을 저장할 CSV 파일 경로
    public string SubjectName;
    public string Direction;
    public int number=1;
    // MVC 값을 저장하는 배열
    private float[] mvcValuesForearm = new float[8];

    private class ButterworthFilter
    {
        private readonly float[] a;
        private readonly float[] b;
        private readonly float[] x;
        private readonly float[] y;

        public ButterworthFilter()
        {
            b = new float[] { 0.2066f, 0f, -0.4131f, 0f, 0.2066f };
            a = new float[] { 1f, -1.3730f, 1.1040f, -0.3433f, 0.0717f };
            x = new float[b.Length];
            y = new float[a.Length];
        }

        public float Apply(float input)
        {
            for (int i = x.Length - 1; i > 0; i--) x[i] = x[i - 1];
            x[0] = input;

            float output = 0;
            for (int i = 0; i < b.Length; i++) output += b[i] * x[i];
            for (int i = 1; i < a.Length; i++) output -= a[i] * y[i - 1];

            for (int i = y.Length - 1; i > 0; i--) y[i] = y[i - 1];
            y[0] = output;

            return output;
        }
    }

    void Start()
    {
        // Find the muscle Transforms
        rightForeArmFlexion = avatarTransform.Find("Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm/Muscle Set/Muscle138");
        rightForeArmExtension = avatarTransform.Find("Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm/Muscle Set/Muscle110");

        if ( rightForeArmFlexion == null || rightForeArmExtension == null)
        {
            Debug.LogError("Failed to find the muscle Transform. Please check the hierarchy.");
        }

        bandpassFilters = new ButterworthFilter[8];
        rectifiedBuffers = new List<float>[8];

        for (int i = 0; i < 8; i++)
        {
            bandpassFilters[i] = new ButterworthFilter();
            rectifiedBuffers[i] = new List<float>();
        }

        recordingData = new List<float>[8];
        mvcValues = new float[8];

        for (int i = 0; i < 8; i++)
        {
            recordingData[i] = new List<float>();
            mvcValues[i] = 0f;
        }
        string mvcArmFilePath = Path.Combine(mvcFilePath + SubjectName + "/", SubjectName + "Arm.csv");

        // MVC 값을 읽고 초기화
        mvcValuesForearm = LoadMVCValuesFromFile(mvcArmFilePath);

        if (mvcValuesForearm == null)
        {
            Debug.LogError("Failed to load MVC values. Ensure the files exist and are correctly formatted.");
        }

        StartServer(); // Start UDP server
    }

    private float[] LoadMVCValuesFromFile(string filePath)
    {
        float[] defaultValues = Enumerable.Repeat(1f, 8).ToArray(); // 기본값 1로 초기화된 배열

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"MVC file not found: {filePath}. Using default values (1) for all channels.");
            return defaultValues; // 파일이 없을 경우 기본값 반환
        }

        float[] mvcValues = new float[8]; // 8개의 채널 값 저장
        string[] lines = File.ReadAllLines(filePath);

        for (int i = 1; i < lines.Length; i++) // 첫 줄은 헤더
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] parts = lines[i].Split(',');
            int channelIndex = int.Parse(parts[0].Replace("Channel ", "").Trim()) - 1;
            float value = float.TryParse(parts[1].Trim(), out float parsedValue) ? parsedValue : 1f;

            mvcValues[channelIndex] = value;
        }

        return mvcValues;
    }

    void Update()
    {
        if (rightForeArmFlexion != null && rightForeArmExtension != null)
        {
            // Apply highlight material
            HighlightRightArm();

            // Visualize muscle activation dynamically
            VisualizeMuscleActivation();

            RealtimeMuscle.DataSource.SetValue("Flexion", "Forearm", forearmFlexionMuscleActivation);
            RealtimeMuscle.DataSource.SetValue("Extension", "Forearm", forearmExtensionMuscleActivation);
        }
        
    }

    private void StartServer()
    {
        isRunning = true;
        udpThread = new Thread(ServerListen);
        udpThread.IsBackground = true;
        udpThread.Start();
        Debug.Log("UDP server started.");
    }

    private List<(long, byte[])> ConvertMultiFrameEMG(byte[] data)
    {
        List<(long, byte[])> result = new List<(long, byte[])>();

        int frameSize = sizeof(long) + 8; // 8-byte timestamp + 8 EMG bytes
        int numFrames = data.Length / frameSize;

        for (int i = 0; i < numFrames; i++)
        {
            int offset = i * frameSize;

            long timestamp = BitConverter.ToInt64(data, offset);
            byte[] emgBytes = new byte[8];
            Array.Copy(data, offset + sizeof(long), emgBytes, 0, 8);

            result.Add((timestamp, emgBytes));
        }

        return result;
    }

    private void ServerListen()
    {
        try
        {
            udpClient = new UdpClient(udpPort);
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, udpPort);

            Debug.Log("Listening for UDP client messages...");

            while (isRunning)
            {
                try
                {
                    // Receive data
                    byte[] data = udpClient.Receive(ref remoteEndPoint);

                    List<(long, byte[])> emgFrames = ConvertMultiFrameEMG(data);

                    foreach (var (unixTime, emgBytes) in emgFrames)
                    {
                        float[] emgFloatValues = emgBytes.Select(b => (float)b).ToArray();  // byte → float
                        UpdateMuscleActivations(emgFloatValues, unixTime);
                        forearmChannels = (unixTime, emgFloatValues); // 마지막 프레임 기준 저장
                        lock (forearmQueue)
                        {
                            forearmQueue.Enqueue((unixTime, emgFloatValues));
                        }

                    }

                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error receiving data: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"UDP server error: {ex.Message}");
        }
    }

    // 🔄 **새로운 메서드 추가**: UnixTime과 EMG 데이터를 분리
    private (long, float[]) ConvertToFloatArrayWithTimestamp(byte[] data)
    {
        if (data.Length < sizeof(long))
        {
            Debug.LogError("Received data is too small to contain a timestamp.");
            return (0, new float[0]);
        }

        // UnixTime을 먼저 읽음 (8바이트)
        long unixTime = BitConverter.ToInt64(data, 0);

        // 이후의 데이터를 EMG 값으로 변환
        int floatCount = (data.Length - sizeof(long)) / sizeof(float);
        float[] emgValues = new float[floatCount];
        Buffer.BlockCopy(data, sizeof(long), emgValues, 0, floatCount * sizeof(float));

        return (unixTime, emgValues);
    }

    private float[] ConvertToFloatArray(byte[] data)
    {
        int floatCount = data.Length / sizeof(float);
        float[] floatArray = new float[floatCount];
        Buffer.BlockCopy(data, 0, floatArray, 0, data.Length);
        return floatArray;
    }

    private void UpdateMuscleActivations(float[] emgValues, long unixTime)
    {
        if (emgValues.Length < 8) return;

        float[] filteredEMG = new float[8];
        float[] rmsEMG = new float[8];
        float[] mvcNormalizedEmg = new float[8];

        for (int i = 0; i < 8; i++)
        {
            filteredEMG[i] = bandpassFilters[i].Apply(emgValues[i]);
            float rectifiedEMG = Mathf.Abs(filteredEMG[i]);

            rectifiedBuffers[i].Add(rectifiedEMG);
            if (rectifiedBuffers[i].Count > windowSize)
            {
                rectifiedBuffers[i].RemoveAt(0);
            }
            rmsEMG[i] = Mathf.Sqrt(rectifiedBuffers[i].Sum(x => x * x) / windowSize);

            // Retain the RMS/MVC ratio. It can legitimately exceed 1.0;
            // display capping happens after group aggregation and smoothing.
            float mvcValue = mvcValuesForearm[i];
            mvcNormalizedEmg[i] = EmgVisualizationMath.NormalizeRmsByMvc(rmsEMG[i], mvcValue);
        }

        if (isRecording)
        {
            for (int i = 0; i < 8; i++)
            {
                recordingData[i].Add(rmsEMG[i]);
                timeStampedRecordingData.Add((unixTime, rmsEMG));
            }
        }

        float normForearmFlexion = GetMvcNormalizedFlexion(mvcNormalizedEmg);
        float normForearmExtension = GetMvcNormalizedExtension(mvcNormalizedEmg);

        forearmFlexionEMA = ApplyExponentialMovingAverage(normForearmFlexion, forearmFlexionEMA, alpha);
        forearmExtensionEMA = ApplyExponentialMovingAverage(normForearmExtension, forearmExtensionEMA, alpha);

        // A stable [0, 1.5] range is for the headset display only, not MVC normalization.
        forearmFlexionMuscleActivation = EmgVisualizationMath.CapForDisplay(
            ApplyThresholdDamping(forearmFlexionEMA, forearmFlexionMuscleActivation, threshold));
        forearmExtensionMuscleActivation = EmgVisualizationMath.CapForDisplay(
            ApplyThresholdDamping(forearmExtensionEMA, forearmExtensionMuscleActivation, threshold));
    }

    private static float GetMvcNormalizedFlexion(float[] mvcNormalizedEmg)
    {
        return EmgVisualizationMath.ComputeGroupRms(
            mvcNormalizedEmg, EmgVisualizationMath.FlexionChannels);
    }

    private static float GetMvcNormalizedExtension(float[] mvcNormalizedEmg)
    {
        return EmgVisualizationMath.ComputeGroupRms(
            mvcNormalizedEmg, EmgVisualizationMath.ExtensionChannels);
    }

    // Apply highlight material to the right arm
    void HighlightRightArm()
    {
        ApplyMaterialToTransform(rightForeArmFlexion, highlightMaterial);
        ApplyMaterialToTransform(rightForeArmExtension, highlightMaterial);
    }

    // Visualize muscle activation dynamically
    void VisualizeMuscleActivation()
    {
        VisualizeMuscleActivationOnTransform(rightForeArmFlexion, forearmFlexionMuscleActivation);
        VisualizeMuscleActivationOnTransform(rightForeArmExtension, forearmExtensionMuscleActivation);
    }

    private void OnApplicationQuit()
    {
        isRunning = false;  // 쓰레드 루프 종료 조건

        if (udpClient != null)
        {
            udpClient.Close();  // 포트 닫기
            udpClient = null;
        }

        if (udpThread != null && udpThread.IsAlive)
        {
            udpThread.Join();  // 쓰레드가 끝날 때까지 대기
            udpThread = null;
        }

        Debug.Log("UDP server stopped.");
    }   

    // Apply material to a specific Transform
    void ApplyMaterialToTransform(Transform targetTransform, Material material)
    {
        Renderer renderer = targetTransform.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = material;
        }
    }

    // Adjust color intensity of the Transform based on activation level
    void VisualizeMuscleActivationOnTransform(Transform targetTransform, float activationLevel)
    {
        Renderer renderer = targetTransform.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Define the color range (low activation -> high activation)
            Color deepRed = new Color(1f, 0f, 0f, 1f);  // Strong red (opacity 1)
            Color lightRed = new Color(1f, 0.8f, 0.8f, 1f); // Light red

            // Interpolate between light and deep red based on activation level
            Color blendedColor = Color.Lerp(lightRed, deepRed, activationLevel);

            // Apply the blended color
            renderer.material.color = blendedColor;
        }
    }

    public (float, float) GetActivationValues()
    {
        return (forearmFlexionMuscleActivation, forearmExtensionMuscleActivation);
    }

    private float ApplyExponentialMovingAverage(float newValue, float prevValue, float alpha)
    {
        return alpha * newValue + (1 - alpha) * prevValue;
    }

    private float ApplyThresholdDamping(float newValue, float prevValue, float threshold)
    {
        return Mathf.Abs(newValue - prevValue) < threshold ? prevValue : newValue;
    }

    public void StartRecording()
    {
        isRecording = true;
        Debug.Log("Forearm MVC recording started.");
        for (int i = 0; i < 8; i++) recordingData[i].Clear();
    }

    public void StopRecording()
    {
        isRecording = false;
        Debug.Log("Forearm MVC recording stopped. Calculating MVC...");

        for (int i = 0; i < 8; i++)
        {
            if (recordingData[i].Count > 0) mvcValues[i] = recordingData[i].Max();
        }

        SaveMVCValuesToCSV();
    }

    private void SaveMVCValuesToCSV()
    {
        string directoryPath = Path.Combine(mvcFilePath, SubjectName);
        string savePath = Path.Combine(directoryPath, SubjectName + "_Forearm_" + Direction + "_" + number.ToString() +".csv"); // 파일 경로

        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

        StringBuilder csvContent = new StringBuilder();
        csvContent.AppendLine("Channel,MVC Value");

        for (int i = 0; i < mvcValues.Length; i++)
        {
            csvContent.AppendLine($"Channel {i + 1},{mvcValues[i]}");
        }

        File.WriteAllText(savePath, csvContent.ToString());
        Debug.Log($"Forearm MVC values saved to {savePath}");
        number += 1;
    }
}
