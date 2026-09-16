using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;
using System.Collections;


public class UserDataRecorder : MonoBehaviour
{
    private bool isRecording = false;
    public string subjectName = "Sample"; // Base name of the saved CSV file
    public int recordingSession = 1; // Session counter to append to the file name
    public float recordInterval = 0.033f; // Time interval for recording (e.g., 30 FPS)

    public List<Transform> jointsToRecord = new List<Transform>(); // List of joints to record
    public ArmMuscleHighlighter armMuscleHighlighter; // Name of the script containing the variables
    public ForearmMuscleHighlighter forearmMuscleHighlighter; // Name of the variable to record

    private List<string> recordedForearmData = new List<string>(); 
    private List<string> recordedArmData = new List<string>(); 
    private List<string> recordedJointData = new List<string>(); 

    public string GetSubjectName() => subjectName;  // Getter 메서드
    public int GetRecordingSession() => recordingSession;  // Getter 메서드

    private float timer = 0f;
    private float startTime;

    // Method to start recording
    // 🎯 **StartRecording을 3초 카운트다운 후 시작하도록 변경**
    public void StartRecording()
    {
        if (jointsToRecord == null || jointsToRecord.Count == 0)
        {
            Debug.LogError("No joints assigned for recording!");
            return;
        }

        StartCoroutine(StartRecordingImmediately());
    }

    private IEnumerator StartRecordingImmediately()
    {
        yield return new WaitForSeconds(0f);

        isRecording = true;
        timer = 0f;
        startTime = Time.time;

        recordedForearmData.Clear();
        recordedArmData.Clear();
        recordedJointData.Clear();
        Debug.Log("Recording started!");
    }

    public void StopRecordingAndSave()
    {
        if (!isRecording)
        {
            Debug.LogWarning("Recording is not active!");
            return;
        }

        isRecording = false;

        // Save data to CSV
        string baseFolderPath = Application.dataPath + "/RecordedData";
        string subjectFolderPath = Path.Combine(baseFolderPath, subjectName);

        // Ensure the subject folder exists
        if (!Directory.Exists(subjectFolderPath))
        {
            Directory.CreateDirectory(subjectFolderPath);
            Debug.Log($"Folder created at: {subjectFolderPath}");
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string filePath = Path.Combine(subjectFolderPath, $"{subjectName}_Session{recordingSession}.csv");

        File.WriteAllLines(Path.Combine(subjectFolderPath, $"{subjectName}_Forearm_Session{recordingSession}.csv"), recordedForearmData);
        File.WriteAllLines(Path.Combine(subjectFolderPath, $"{subjectName}_Arm_Session{recordingSession}.csv"), recordedArmData);
        File.WriteAllLines(Path.Combine(subjectFolderPath, $"{subjectName}_Joint_Session{recordingSession}.csv"), recordedJointData);
        List<string> replayPayload = BuildReplayPayload();
        if (replayPayload.Count == 90)
        {
            File.WriteAllLines(filePath, replayPayload);
        }
        else
        {
            Debug.LogError("Recorded replay payload was not written because it could not be converted to 90 valid frames.");
        }


        Debug.Log($"Recording stopped. Data saved to: {filePath}");

        // Increment session for future recordings
        recordingSession++;
    }

    private void FixedUpdate()
    {
        if (isRecording)
        {
            timer += Time.fixedDeltaTime;

            // Record data at specified intervals
            if (timer >= recordInterval)
            {
                RecordFrameData();
                timer = 0f;
            }
        }
    }

    // 이전 Unix Time 저장용 변수
    private long prevForearmUnixTime = -1;
    private long prevArmUnixTime = -1;

    // Record data for the current frame
    private void RecordFrameData()
    {
        // ✅ UnixTime 변환 (현재 시스템 시간 기준)
        long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        

        if (forearmMuscleHighlighter != null)
        {
            lock (forearmMuscleHighlighter.forearmQueue)
            {
                while (forearmMuscleHighlighter.forearmQueue.Count > 0)
                {
                    var (unixTime, emgData) = forearmMuscleHighlighter.forearmQueue.Dequeue();
                    string line = $"{unixTime}," + string.Join(",", emgData.Select(v => v.ToString("F4")));
                    recordedForearmData.Add(line);
                }
            }
        }
        else
        {
            recordedForearmData.Add($"0," + string.Join(",", new float[8])); // Default values
        }

        
        if (armMuscleHighlighter != null)
        {
            lock (armMuscleHighlighter.armQueue)
            {
                while (armMuscleHighlighter.armQueue.Count > 0)
                {
                    var (unixTime, emgData) = armMuscleHighlighter.armQueue.Dequeue();
                    string line = $"{unixTime}," + string.Join(",", emgData.Select(v => v.ToString("F4")));
                    recordedArmData.Add(line);
                }
            }
        }
        else
        {
            recordedArmData.Add($"0," + string.Join(",", new float[8])); // Default values
        }

        

        // ✅ **Joint 데이터 저장 (이제 UnixTime 기반)**
        string jointFrameData = $"{currentUnixTime}";

        foreach (var joint in jointsToRecord)
        {
            if (joint != null)
            {
                Vector3 position = joint.position;

                jointFrameData  += $",{position.x:F4},{position.y:F4},{position.z:F4}";
            }
            else
            {
                jointFrameData  += ",0,0,0"; // Default values if joint is null
            }
        }

        foreach (var joint in jointsToRecord)
        {
            if (joint != null)
            {
                Vector3 position = joint.localPosition;

                jointFrameData  += $",{position.x:F4},{position.y:F4},{position.z:F4}";
            }
            else
            {
                jointFrameData  += ",0,0,0"; // Default values if joint is null
            }
        }

        foreach (var joint in jointsToRecord)
        {
            if (joint != null)
            {
                Quaternion rotation = joint.localRotation;
                jointFrameData  += $",{rotation.x:F4},{rotation.y:F4},{rotation.z:F4},{rotation.w:F4}";
            }
            else
            {
                jointFrameData  += ",0,0,0,0"; // Default values if joint is null
            }
        }

        foreach (var joint in jointsToRecord)
        {
            if (joint != null)
            {
                Quaternion rotation = joint.rotation;
                jointFrameData  += $",{rotation.x:F4},{rotation.y:F4},{rotation.z:F4},{rotation.w:F4}";
            }
            else
            {
                jointFrameData  += ",0,0,0,0"; // Default values if joint is null
            }
        }
        

        recordedJointData.Add(jointFrameData);
    }


    // Check if the recorder is currently recording
    public bool IsRecording()
    {
        return isRecording;
    }

    /// <summary>
    /// Builds the exact payload consumed by the AutoEncoder and replay engine:
    /// [lower EMG 8, upper EMG 8, global position 63, local position 63,
    /// local quaternion 84]. Raw sensor logs remain separately available.
    /// </summary>
    private List<string> BuildReplayPayload()
    {
        List<float[]> frames = new List<float[]>();
        foreach (string jointLine in recordedJointData)
        {
            string[] jointValues = jointLine.Split(',');
            // timestamp + 63 global positions + 63 local positions + 84 local
            // quaternions. The following global quaternion block is not used
            // by the replayer and deliberately stays out of the payload.
            if (jointValues.Length < 211 || !long.TryParse(jointValues[0], out long timestamp))
                continue;

            float[] frame = new float[226];
            float[] forearm = FindNearestEmgFrame(recordedForearmData, timestamp);
            float[] arm = FindNearestEmgFrame(recordedArmData, timestamp);
            Array.Copy(forearm, 0, frame, 0, 8);
            Array.Copy(arm, 0, frame, 8, 8);

            bool valid = true;
            for (int sourceColumn = 1; sourceColumn <= 210; sourceColumn++)
            {
                if (!TryParseFloat(jointValues[sourceColumn], out float value))
                {
                    valid = false;
                    break;
                }
                frame[16 + sourceColumn - 1] = value;
            }

            if (valid)
                frames.Add(frame);
        }

        if (frames.Count < 2)
            return new List<string>();

        List<string> output = new List<string>(90);
        for (int target = 0; target < 90; target++)
        {
            float position = target * (frames.Count - 1) / 89f;
            int lower = Mathf.FloorToInt(position);
            int upper = Mathf.Min(lower + 1, frames.Count - 1);
            float fraction = position - lower;
            float[] resampled = new float[226];
            for (int column = 0; column < resampled.Length; column++)
                resampled[column] = Mathf.Lerp(frames[lower][column], frames[upper][column], fraction);
            NormalizeQuaternionBlocks(resampled);
            output.Add(string.Join(",", resampled.Select(value => value.ToString("F6", CultureInfo.InvariantCulture))));
        }
        return output;
    }

    private static float[] FindNearestEmgFrame(List<string> emgFrames, long timestamp)
    {
        float[] nearestValues = new float[8];
        long nearestDistance = long.MaxValue;
        foreach (string line in emgFrames)
        {
            string[] values = line.Split(',');
            if (values.Length < 9 || !long.TryParse(values[0], out long emgTimestamp))
                continue;
            long distance = Math.Abs(emgTimestamp - timestamp);
            if (distance >= nearestDistance)
                continue;
            float[] parsed = new float[8];
            bool valid = true;
            for (int channel = 0; channel < 8; channel++)
            {
                if (!TryParseFloat(values[channel + 1], out parsed[channel]))
                {
                    valid = false;
                    break;
                }
            }
            if (valid)
            {
                nearestDistance = distance;
                nearestValues = parsed;
            }
        }
        return nearestValues;
    }

    private static bool TryParseFloat(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static void NormalizeQuaternionBlocks(float[] frame)
    {
        const int localQuaternionStart = 142;
        for (int joint = 0; joint < 21; joint++)
        {
            int index = localQuaternionStart + joint * 4;
            Quaternion rotation = new Quaternion(
                frame[index], frame[index + 1], frame[index + 2], frame[index + 3]);
            if (Quaternion.Dot(rotation, rotation) > 0.000001f)
            {
                rotation = Quaternion.Normalize(rotation);
                frame[index] = rotation.x;
                frame[index + 1] = rotation.y;
                frame[index + 2] = rotation.z;
                frame[index + 3] = rotation.w;
            }
        }
    }
}
