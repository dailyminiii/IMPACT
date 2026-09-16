#define Graph_And_Chart_PRO
using System;
using UnityEngine;
using NeuronDataReaderManaged;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using MixedReality.Toolkit.UX; // MRTK UI 사용
using TMPro; // TextMeshPro 사용
using ChartAndGraph;
using Unity.VisualScripting;

namespace Neuron
{
	/// <summary>
	/// Shared replay engine for both strokes. Stroke-specific differences are
	/// limited to the EMG channel aggregation selected by <see cref="isForehand"/>.
	/// </summary>
	public class IMPACTReplay : NeuronInstance
	{
        private scaleController scaleControllerInstance;

        [Header("Stroke Type")]
        [Tooltip("True: forehand channels; false: backhand channels. Motion replay, DTW alignment, colours, scale, and chart ranges are otherwise shared.")]
        public bool isForehand = true;

        [Space(10)]
        public bool enableHipMove = true;
        public bool enableFingerMove = true;
        public Transform					root_expert = null;
        public Transform					root_user = null;

        public string folderPath = "Assets/FeedbackSample/";
        public string subject = "Sub00";
        public int strokeNumber = 00;

        [Header("Public demo fallback")]
        [Tooltip("Demo-only: when the selected user recording has no EMG samples, synthesize a visibly distinct trace from the matched expert EMG. This never writes to the recorded CSV or represents participant data.")]
        public bool synthesizeUserEmgForDemoWhenMissing = false;
        private bool isUsingSyntheticUserDemoEmg;

        // Obsolete don't use it
        [HideInInspector]
		public string						prefix_expert = "Robot_";
        public string						prefix_user = "Robot_";
		public bool							boundTransforms { get ; private set; }
		public UpdateMethod					motionUpdateMethod = UpdateMethod.Normal;

        public Transform[]					transforms_expert = new Transform[(int)NeuronBones_simple.NumOfBones];
        public Transform[]					transforms_user = new Transform[(int)NeuronBones_simple.NumOfBones];

        [Header("use an already existing NeuronTransformsInstance as the physical reference source")]
        [HideInInspector]
        public Transform					physicalReferenceOverride; //use an already existing NeuronAnimatorInstance as the physical reference

        [SerializeField] private UserDataRecorder UserDataRecorder;


        [Header("Guidance control variables")]
		[Range(0.1f, 1f)]
        public Vector3 positionOffset_Expert = new Vector3(0f, 0f, 0f);
        public Vector3 positionOffset_User = new Vector3(0f, 0f, 0f);

        public int windowSize;
        private int TotalPoints;

        public GameObject Hips_Expert;
        public GameObject Hips_User;
        public GameObject Player;

        private ButterworthFilter[] bandpassFilters;

        public float alpha;
        public float threshold;
        private bool isReplaying = false; // ✅ 현재 재생 중인지 체크하는 플래그
        public bool IsReplaying => isReplaying;
        public event System.Action OnReplayFinished;

        private speedController speedCtrl; // speedController 인스턴스 저장
        public float speedScaler; // 기본 속도 스케일러 값

        [SerializeField] private PressableButton startButton; // MRTK PressableButton
        [SerializeField] private TextMeshProUGUI countdownText; // 카운트다운 표시 UI
        [SerializeField] private AudioSource ttsAudioSource; // AudioSource (TTS 재생)
        [SerializeField] private AudioClip[] countdownClips; // "5, 4, 3, 2, 1, Go!" 오디오 클립 배열


        private Dictionary<int, float[]> ArmMuscle_Expert = new Dictionary<int, float[]>();
        private Dictionary<int, float[]> ArmMuscle_User = new Dictionary<int, float[]>();

        // Graph Chart

        public GraphChart ForearmFlexionMuscle;  // ✅ GraphChart 추가
        public GraphChart ForearmExtensionMuscle;  // ✅ GraphChart 추가
        public GraphChart ArmFlexionMuscle;  // ✅ GraphChart 추가
        public GraphChart ArmExtensionMuscle;  // ✅ GraphChart 추가

        private float xStep;
        private int currentPoint = 0; // ✅ 현재 추가된 포인트 개수
        private bool isUpdating = false; // ✅ 업데이트 진행 여부 플래그

        // Graph Chart for Stat
        public CanvasBarChart RMS_Max_B;  // ✅ GraphChart 추가
        public CanvasBarChart RMS_Mean_B;  // ✅ GraphChart 추가
        public CanvasBarChart OnsetTime_B;  // ✅ GraphChart 추가
        public CanvasBarChart OffsetTime_B;  // ✅ GraphChart 추가
        public CanvasBarChart Duration_B;  // ✅ GraphChart 추가
        public CanvasBarChart Coactivation_B;  // ✅ GraphChart 추가

        public CanvasBarChart RMS_Max_H;  // ✅ GraphChart 추가
        public CanvasBarChart RMS_Mean_H;  // ✅ GraphChart 추가
        public CanvasBarChart OnsetTime_H;  // ✅ GraphChart 추가
        public CanvasBarChart OffsetTime_H;  // ✅ GraphChart 추가
        public CanvasBarChart Duration_H;  // ✅ GraphChart 추가
        public CanvasBarChart Coactivation_H;  // ✅ GraphChart 추가

        private List<float> ForearmFlexionMuscleValues_expert = new List<float>(); // ✅ EMG 데이터를 저장할 리스트
        private List<float> ForearmExtensionMuscleValues_expert = new List<float>(); // ✅ EMG 데이터를 저장할 리스트
        private List<float> ArmFlexionMuscleValues_expert = new List<float>(); // ✅ EMG 데이터를 저장할 리스트
        private List<float> ArmExtensionMuscleValues_expert = new List<float>(); // ✅ EMG 데이터를 저장할 리스트

        private List<float> ForearmFlexionMuscleValues_user = new List<float>(); // ✅ EMG 데이터를 저장할 리스트
        private List<float> ForearmExtensionMuscleValues_user = new List<float>(); // ✅ EMG 데이터를 저장할 리스트
        private List<float> ArmFlexionMuscleValues_user = new List<float>(); // ✅ EMG 데이터를 저장할 리스트
        private List<float> ArmExtensionMuscleValues_user = new List<float>(); // ✅ EMG 데이터를 저장할 리스트




        public NeuronTransformsPhysicalReference	physicalReference_Expert = new NeuronTransformsPhysicalReference();
		Vector3[]							bonePositionOffsets_Expert = new Vector3[(int)NeuronBones_simple.NumOfBones];
		Vector3[]							boneRotationOffsets_Expert = new Vector3[(int)NeuronBones_simple.NumOfBones];

        Quaternion[] orignalRot_Expert = new Quaternion[(int)NeuronBones_simple.NumOfBones];
        Quaternion[] orignalParentRot_Expert = new Quaternion[(int)NeuronBones_simple.NumOfBones];
        Vector3[] orignalPositions_Expert = new Vector3[(int)NeuronBones_simple.NumOfBones];

        [HideInInspector]
        public bool[] disableBoneMovement_Expert = new bool[(int)NeuronBones_simple.NumOfBones];


		// Data extracted from CSV file were saved 
		private Dictionary<int, Quaternion[]> jointRotationData_Expert = new Dictionary<int, Quaternion[]>();
        private Dictionary<int, Vector3[]> jointPositionData_Expert = new Dictionary<int, Vector3[]>();
        private Dictionary<int, Vector3[]> jointGlobalPositionData_Expert = new Dictionary<int, Vector3[]>();
        public NeuronTransformsPhysicalReference	physicalReference_User = new NeuronTransformsPhysicalReference();
		Vector3[]							bonePositionOffsets_User = new Vector3[(int)NeuronBones_simple.NumOfBones];
		Vector3[]							boneRotationOffsets_User = new Vector3[(int)NeuronBones_simple.NumOfBones];

        Quaternion[] orignalRot_User = new Quaternion[(int)NeuronBones_simple.NumOfBones];
        Quaternion[] orignalParentRot_User = new Quaternion[(int)NeuronBones_simple.NumOfBones];
        Vector3[] orignalPositions_User = new Vector3[(int)NeuronBones_simple.NumOfBones];

        [HideInInspector]
        public bool[] disableBoneMovement_User = new bool[(int)NeuronBones_simple.NumOfBones];


		// Data extracted from CSV file were saved 
		private Dictionary<int, Quaternion[]> jointRotationData_User = new Dictionary<int, Quaternion[]>();
        private Dictionary<int, Vector3[]> jointPositionData_User = new Dictionary<int, Vector3[]>();
        private Dictionary<int, Vector3[]> jointGlobalPositionData_User = new Dictionary<int, Vector3[]>();


        bool inited = false;

        private int feedbackStartFrame = 0; //슬라이더 값으로 설정될 시작 프레임 (UI)

        private void Start()
        {

            scaleControllerInstance = FindObjectOfType<scaleController>(); // scaleController 인스턴스 찾기
            if (scaleControllerInstance == null)
            {
                Debug.LogError("scaleController not found in the scene!");
            }

            // speedController 인스턴스를 찾기
            speedCtrl = FindObjectOfType<speedController>();
            if (speedCtrl == null)
            {
                Debug.LogError("speedController not found in the scene!");
            }

            ClearReplayGraphs();
        }

        new void OnEnable()
        {
            if (inited)
            {
                return;
            }
            inited = true;

            // base.OnEnable();

            // Assign the first element of the arrays to root_expert and root_user if they are not null
            if (root_expert == null && transforms_expert != null && transforms_expert.Length > 0)
            {
                root_expert = transforms_expert[0];
            }

            if (root_user == null && transforms_user != null && transforms_user.Length > 0)
            {
                root_user = transforms_user[0];
            }

            BindExpert(root_expert, prefix_expert);
            BindUser(root_user, prefix_user);
        }

        void Update()
        {

            if (UserDataRecorder == null)
            {
                Debug.LogError("UserDataRecorder is not assigned");
                return;
            }

            subject = UserDataRecorder.GetSubjectName();
            // strokeNumber = UserDataRecorder.GetRecordingSession();
            


            if (speedCtrl != null)
            {
                speedScaler = speedCtrl.SpeedScaler; // speedScaler 값 업데이트
            }

            
            
        }

        //시작 프레임 값을 업데이트하는 메서드
        public void SetFeedbackStartFrame(int frame) {
            feedbackStartFrame = Mathf.Clamp(frame, 0, 299);
            Debug.Log($"Feedback Start Frame Updated: {feedbackStartFrame}");
        }

        private float harshnessFactor; // 0.05 정도 추천

        // public class DecayedDTW
        // {
        //     private float decayFactor;

        //     public DecayedDTW(float decayFactor = 0.9f)
        //     {
        //         this.decayFactor = decayFactor;
        //     }

        //     public List<(int expertFrame, int userFrame)> ComputeFullPath(float[,] costMatrix)
        //     {
        //         int n = costMatrix.GetLength(0); // Expert
        //         int m = costMatrix.GetLength(1); // User

        //         float[,] dp = new float[n, m];
        //         int[,] path = new int[n, m];

        //         dp[0, 0] = costMatrix[0, 0];
        //         for (int i = 1; i < n; i++) dp[i, 0] = dp[i - 1, 0] + costMatrix[i, 0];
        //         for (int j = 1; j < m; j++) dp[0, j] = dp[0, j - 1] + costMatrix[0, j];

        //         for (int i = 1; i < n; i++)
        //         {
        //             for (int j = 1; j < m; j++)
        //             {
        //                 float decay = Mathf.Pow(decayFactor, Mathf.Abs(i - j));
        //                 float minPrev = Mathf.Min(dp[i - 1, j - 1], Mathf.Min(dp[i - 1, j], dp[i, j - 1]));
        //                 dp[i, j] = costMatrix[i, j] + decay * minPrev;

        //                 if (minPrev == dp[i - 1, j - 1]) path[i, j] = 0;
        //                 else if (minPrev == dp[i - 1, j]) path[i, j] = 1;
        //                 else path[i, j] = 2;
        //             }
        //         }

        //         // 경로 추적
        //         List<(int, int)> fullPath = new List<(int, int)>();
        //         int x = n - 1, y = m - 1;
        //         while (x > 0 && y > 0)
        //         {
        //             fullPath.Add((x, y));
        //             int dir = path[x, y];
        //             if (dir == 0) { x--; y--; }
        //             else if (dir == 1) { x--; }
        //             else { y--; }
        //         }
        //         fullPath.Add((0, 0));
        //         fullPath.Reverse();
        //         return fullPath;
        //     }

        //     public List<(int expertFrame, int userFrame)> ComputeFullPath(List<float> expertDistances, List<float> userDistances)
        //     {
        //         int n = expertDistances.Count;
        //         int m = userDistances.Count;
        //         float[,] costMatrix = new float[n, m];

        //         for (int i = 0; i < n; i++)
        //         {
        //             for (int j = 0; j < m; j++)
        //             {
        //                 costMatrix[i, j] = Mathf.Abs(expertDistances[i] - userDistances[j]); // 거리 차이 기반 비용
        //             }
        //         }

        //         return ComputeFullPath(costMatrix); // 기존에 있는 함수 재사용
        //     }

        // }

        private void SetObjectActive(GameObject obj, bool isActive)
        {
            if (obj != null)
            {
                obj.SetActive(isActive);
            }
            else
            {
                Debug.LogWarning("Target feedback object is not assigned.");
            }
        }

        private bool UserReplayEmgIsSilent()
        {
            // A recording without valid MVC values used to create NaN here (0 / 0).
            // Treat non-finite entries as absent data so the public demo can use its
            // clearly labelled synthetic trace instead of feeding NaN into the chart.
            return ArmMuscle_User.Count > 0 && ArmMuscle_User.Values.All(frame => frame.All(value =>
                float.IsNaN(value) || float.IsInfinity(value) || Mathf.Abs(value) < 0.000001f));
        }

        private void PopulateSyntheticDemoUserEmg()
        {
            List<int> expertFrames = ArmMuscle_Expert.Keys.OrderBy(frame => frame).ToList();
            List<int> userFrames = ArmMuscle_User.Keys.OrderBy(frame => frame).ToList();
            if (expertFrames.Count == 0 || userFrames.Count == 0)
            {
                return;
            }

            // Fixed, channel-specific gains and phase shifts keep the demonstration trace
            // recognizably related to the matched expert while preventing it from being a copy.
            float[] gains = { 0.88f, 1.04f, 0.92f, 1.07f, 0.84f, 1.02f, 0.90f, 1.06f,
                              0.91f, 1.05f, 0.87f, 1.03f, 0.94f, 1.00f, 0.89f, 1.08f };

            for (int userIndex = 0; userIndex < userFrames.Count; userIndex++)
            {
                float phase = userFrames.Count == 1 ? 0f : (float)userIndex / (userFrames.Count - 1);
                float[] synthetic = new float[16];
                for (int channel = 0; channel < synthetic.Length; channel++)
                {
                    float channelPhase = Mathf.Clamp01(phase + ((channel % 4) - 1.5f) * 0.015f);
                    float expertIndex = channelPhase * (expertFrames.Count - 1);
                    int lowIndex = Mathf.FloorToInt(expertIndex);
                    int highIndex = Mathf.Min(lowIndex + 1, expertFrames.Count - 1);
                    float expertActivation = Mathf.Lerp(
                        ArmMuscle_Expert[expertFrames[lowIndex]][channel],
                        ArmMuscle_Expert[expertFrames[highIndex]][channel],
                        expertIndex - lowIndex);
                    float modulation = 0.018f * Mathf.Sin((phase * 2f * Mathf.PI * (1.2f + channel * 0.03f)) + channel);
                    // The synthetic public-demo trace uses the same display
                    // ceiling as both real replay traces. This is display-only
                    // clipping; it is not MVC normalization.
                    synthetic[channel] = Mathf.Clamp(
                        (expertActivation * gains[channel]) + modulation,
                        0f,
                        EmgVisualizationMath.DisplayMaximum);
                }
                ArmMuscle_User[userFrames[userIndex]] = synthetic;
            }

            isUsingSyntheticUserDemoEmg = true;
            Debug.LogWarning("DEMO ONLY: The selected user recording has no EMG samples. Displaying synthetic user EMG derived from the matched expert trace; recorded CSV files remain unchanged.");
        }

        public void ActivateReplay()
        {
            if (isReplaying) return; // ✅ 이미 재생 중이면 실행 안 함

            // Base path where CSV files are stored
            string basePath = $"{subject}/";
            string subCode;

            // The public replay uses a separately versioned calibration reference.
            // The public demo uses the reproducible v2 expert calibrations.
            string ExpertMVCbasePath = $"ExpertMVC/";

            // Find the matching expert file
            string ExpertCSV = FindMatchingExpertCSV(basePath, subject, strokeNumber, out subCode);
            if (string.IsNullOrEmpty(ExpertCSV))
            {
                Debug.LogError("No matching expert file found. Cannot proceed.");
                return;
            }


            // User file path
            string UserCSV = $"{basePath}{subject}_Session{strokeNumber}.csv";
            string UserMVCForearm = $"{basePath}{subject}Forearm.csv";
            string UserMVCArm = $"{basePath}{subject}Arm.csv";

            string ExpertMVCForearm = $"{ExpertMVCbasePath}{subCode}Forearm.csv";
            string ExpertMVCArm = $"{ExpertMVCbasePath}{subCode}Arm.csv";

            bool expertLoaded = ReadCSV_Expert(ExpertCSV, ExpertMVCForearm, ExpertMVCArm, jointRotationData_Expert, jointPositionData_Expert);
            bool userLoaded = ReadCSV_User(UserCSV, UserMVCForearm, UserMVCArm, jointRotationData_User, jointPositionData_User);
            isUsingSyntheticUserDemoEmg = false;
            if (expertLoaded && userLoaded && synthesizeUserEmgForDemoWhenMissing && UserReplayEmgIsSilent())
            {
                PopulateSyntheticDemoUserEmg();
            }
            if (!expertLoaded || !userLoaded || jointRotationData_Expert.Count == 0 || jointRotationData_User.Count == 0)
            {
                Debug.LogError("Replay did not start because the expert or user replay payload could not be loaded.");
                jointRotationData_Expert.Clear();
                jointPositionData_Expert.Clear();
                jointRotationData_User.Clear();
                jointPositionData_User.Clear();
                return;
            }

            if (!boundTransforms)
            {
                Debug.LogError("Replay did not start because the expert and user avatar transforms are not bound.");
                return;
            }

            isReplaying = true;
            Debug.Log($"Replay payload loaded: expert={Path.GetFileName(ExpertCSV)} ({jointRotationData_Expert.Count} frames), user={Path.GetFileName(UserCSV)} ({jointRotationData_User.Count} frames).");
            Normalization();

            // ✅ DTW 정렬된 프레임 인덱스 가져오기 (최소 거리 기반)
            // ✅ 전문가와 사용자 Joint 기반 DTW 정렬된 전체 매핑 경로 받아오기
            // var matchedPath = PerformDTWAlignment_Joint16XOnly(jointGlobalPositionData_Expert, jointGlobalPositionData_User, threshold=0.04f);


            StartMotion(ExpertCSV, UserCSV, "replay");

        }


        // private List<(int expertFrame, int userFrame)> PerformDTWAlignment_Joint16XOnly(
        //     Dictionary<int, Vector3[]> expertPositions,
        //     Dictionary<int, Vector3[]> userPositions,
        //     float threshold = 0.04f) // ← 얼마나 급격해야 "변화"로 볼 것인지
        // {
        //     int targetJointIndex = 16;

        //     // 1. user의 joint16의 x값 변화량 계산
        //     List<int> userFrames = userPositions.Keys.OrderBy(f => f).ToList();
        //     int userStartFrame = 0;

        //     // 1. 변화량 리스트 저장
        //     List<float> deltaList = new List<float>();
        //     Dictionary<int, float> deltaByFrame = new Dictionary<int, float>();

        //     for (int i = 1; i < userFrames.Count; i++)
        //     {
        //         int prevFrame = userFrames[i - 1];
        //         int currFrame = userFrames[i];

        //         if (userPositions[prevFrame].Length <= targetJointIndex || userPositions[currFrame].Length <= targetJointIndex)
        //             continue;

        //         float xPrev = userPositions[prevFrame][targetJointIndex].x;
        //         float xCurr = userPositions[currFrame][targetJointIndex].x;
        //         float delta = Mathf.Abs(xCurr - xPrev);

        //         deltaList.Add(delta);
        //         deltaByFrame[currFrame] = delta;

        //         if (delta >= threshold && userStartFrame == 0)
        //         {
        //             userStartFrame = currFrame;
        //         }
        //     }

        //     // 2. 통계 출력
        //     if (deltaList.Count > 0)
        //     {
        //         float mean = deltaList.Average();
        //         float std = Mathf.Sqrt(deltaList.Average(v => Mathf.Pow(v - mean, 2)));
        //         float max = deltaList.Max();
        //         int maxDeltaFrame = deltaByFrame.FirstOrDefault(kv => kv.Value == max).Key;

        //         Debug.Log($"📊 Joint16 X 변화량 통계 (Δx):");
        //         Debug.Log($"   • 평균 (mean): {mean:F4}");
        //         Debug.Log($"   • 표준편차 (std): {std:F4}");
        //         Debug.Log($"   • 최대 변화량 (max): {max:F4} @ Frame {maxDeltaFrame}");
        //     }

        //     Debug.Log($"⏩ User DTW 시작 프레임: {userStartFrame}");

        //     int n = expertPositions.Keys.Max() + 1;
        //     int m = userPositions.Keys.Max() + 1;

        //     float[,] costMatrix = new float[n, m];

        //     for (int i = 0; i < n; i++)
        //     {
        //         if (!expertPositions.ContainsKey(i)) continue;
        //         Vector3[] expertJoints = expertPositions[i];

        //         for (int j = userStartFrame; j < m; j++)
        //         {
        //             if (!userPositions.ContainsKey(j)) continue;
        //             Vector3[] userJoints = userPositions[j];

        //             if (expertJoints.Length <= targetJointIndex || userJoints.Length <= targetJointIndex)
        //                 continue;

        //             float expertX = expertJoints[targetJointIndex].x;
        //             float userX = userJoints[targetJointIndex].x;

        //             costMatrix[i, j] = Mathf.Abs(expertX - userX);
        //         }
        //     }

        //     // ✅ DTW 실행
        //     DecayedDTW dtw = new DecayedDTW(0.85f);
        //     List<(int expertFrame, int userIndex)> path = dtw.ComputeFullPath(costMatrix);

        //     List<(int expertFrame, int userFrame)> adjustedPath = path
        //         .Select(p => (p.Item1, p.Item2 + userStartFrame))  // ✅ userFrame 보정
        //         .ToList();

        //     return adjustedPath;
        // }





        private List<Vector3> GetRelativeVectors(Dictionary<int, Vector3[]> data, int jointIndex)
        {
            List<Vector3> relVecs = new List<Vector3>();
            int maxFrame = data.Keys.Max();

            for (int i = 0; i <= maxFrame; i++)
            {
                if (!data.ContainsKey(i))
                {
                    relVecs.Add(Vector3.zero);
                    continue;
                }

                Vector3[] joints = data[i];
                if (joints.Length <= jointIndex || joints[0] == Vector3.zero || joints[jointIndex] == Vector3.zero)
                {
                    relVecs.Add(Vector3.zero);
                    continue;
                }

                Vector3 relative = joints[jointIndex] - joints[0]; // Joint - Hip
                relVecs.Add(relative);
            }

            return relVecs;
        }




        float ComputeElbowAngle(Vector3 shoulder, Vector3 elbow, Vector3 wrist)
        {
            Vector3 vec1 = shoulder - elbow; // upper arm
            Vector3 vec2 = wrist - elbow;    // forearm
            return Vector3.Angle(vec1, vec2); // in degrees
        }



        public void StopReplay()
        {
            if (!isReplaying) return; // ✅ 재생 중이 아닐 때는 실행하지 않음

            isReplaying = false; // ✅ 재생 상태 OFF
            StopAllCoroutines(); // ✅ 모든 코루틴 정지 (모션 재생 중단)

            Debug.Log("Replay stopped!");

            // ✅ 현재 프레임 초기화 (첫 프레임으로 리셋)
            feedbackStartFrame = 0;

            // ✅ 전문가 & 사용자 데이터 초기화
            jointRotationData_Expert.Clear();
            jointPositionData_Expert.Clear();
            jointRotationData_User.Clear();
            jointPositionData_User.Clear();

            // ✅ 그래프도 초기화 (모든 데이터 0으로 설정)
            ResetGraph();
        }

        // ✅ 그래프 데이터 리셋 함수 추가
        private void ResetGraph()
        {
            ForearmFlexionMuscle.DataSource.StartBatch();
            ForearmFlexionMuscle.DataSource.ClearCategory("expert");
            ForearmFlexionMuscle.DataSource.ClearCategory("user");

            ForearmExtensionMuscle.DataSource.StartBatch();
            ForearmExtensionMuscle.DataSource.ClearCategory("expert");
            ForearmExtensionMuscle.DataSource.ClearCategory("user");

            ArmFlexionMuscle.DataSource.StartBatch();
            ArmFlexionMuscle.DataSource.ClearCategory("expert");
            ArmFlexionMuscle.DataSource.ClearCategory("user");

            ArmExtensionMuscle.DataSource.StartBatch();
            ArmExtensionMuscle.DataSource.ClearCategory("expert");
            ArmExtensionMuscle.DataSource.ClearCategory("user");

            for (int i = 0; i < TotalPoints; i++)
            {
                float x = i * xStep;
                ForearmFlexionMuscle.DataSource.AddPointToCategory("expert", x, 0f);
                ForearmFlexionMuscle.DataSource.AddPointToCategory("user", x, 0f);

                ForearmExtensionMuscle.DataSource.AddPointToCategory("expert", x, 0f);
                ForearmExtensionMuscle.DataSource.AddPointToCategory("user", x, 0f);

                ArmFlexionMuscle.DataSource.AddPointToCategory("expert", x, 0f);
                ArmFlexionMuscle.DataSource.AddPointToCategory("user", x, 0f);

                ArmExtensionMuscle.DataSource.AddPointToCategory("expert", x, 0f);
                ArmExtensionMuscle.DataSource.AddPointToCategory("user", x, 0f);
            }

            ForearmFlexionMuscle.DataSource.EndBatch();
            ForearmExtensionMuscle.DataSource.EndBatch();
            ArmFlexionMuscle.DataSource.EndBatch();
            ArmExtensionMuscle.DataSource.EndBatch();
        }

        /// <summary>
        /// Clears the replay traces without inserting placeholder values. Placeholder zeroes
        /// previously occupied every x-position and obscured the EMG samples appended during
        /// replay, especially the white user trace.
        /// </summary>
        private void ClearReplayGraphs()
        {
            ClearReplayGraph(ForearmFlexionMuscle);
            ClearReplayGraph(ForearmExtensionMuscle);
            ClearReplayGraph(ArmFlexionMuscle);
            ClearReplayGraph(ArmExtensionMuscle);

            ForearmFlexionMuscleValues_expert.Clear();
            ForearmFlexionMuscleValues_user.Clear();
            ForearmExtensionMuscleValues_expert.Clear();
            ForearmExtensionMuscleValues_user.Clear();
            ArmFlexionMuscleValues_expert.Clear();
            ArmFlexionMuscleValues_user.Clear();
            ArmExtensionMuscleValues_expert.Clear();
            ArmExtensionMuscleValues_user.Clear();
        }

        private static void ClearReplayGraph(GraphChart graph)
        {
            if (graph == null)
            {
                return;
            }

            graph.DataSource.StartBatch();
            graph.DataSource.ClearCategory("expert");
            graph.DataSource.ClearCategory("user");
            graph.DataSource.EndBatch();
        }

        private void PrepareReplayMuscleGraphs(int replayFrameCount)
        {
            TotalPoints = Mathf.Max(1, replayFrameCount);
            xStep = TotalPoints > 1 ? 10f / (TotalPoints - 1) : 0f;
            ClearReplayGraphs();
        }

        private string FindMatchingExpertCSV(string directory, string subject, int strokeNumber, out string subCode)
        {
            subCode = string.Empty; // Initialize the output variable

            // Get the full path of the directory
            string fullPath = Path.Combine(folderPath, directory);
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"Directory not found: {fullPath}");
                return string.Empty;
            }

            // Get all files in the directory
            string[] allFiles = Directory.GetFiles(fullPath).OrderBy(Path.GetFileName).ToArray();

            // The matching service writes the expert identity selected by the
            // AutoEncoder.  Do not expose a manual expert selector here: that
            // would bypass the study's motion-driven retrieval step.
            string pattern = $"{Regex.Escape(subject)}_Session{strokeNumber}_expert_(Sub\\d{{2}})\\.csv";
            foreach (string file in allFiles)
            {
                // Match the file name against the pattern
                Match match = Regex.Match(Path.GetFileName(file), pattern); // Use Path.GetFileName to match only the file name
                if (match.Success)
                {
                    subCode = match.Groups[1].Value; // Extract the SubXX part
                    return file; // Return the first matching file
                }
            }

            Debug.LogError($"No AutoEncoder-matched expert file found for {subject} Session {strokeNumber}. Start the stroke-specific matcher and request retrieval before replay.");
            return string.Empty;
        }

        private bool ReadCSV_Expert(string fileName, string mvcforearm, string mvcarm, Dictionary<int, Quaternion[]> jointRotations, Dictionary<int, Vector3[]> jointPositions)
        {
            string fullPathMVCForearm = Path.Combine(folderPath, mvcforearm);
            string fullPathMVCArm = Path.Combine(folderPath, mvcarm);


            if (!File.Exists(fileName))
            {
                Debug.LogError($"File not found: {fileName}");
                return false;
            }

            if (!File.Exists(fullPathMVCForearm) || !File.Exists(fullPathMVCArm))
            {
                Debug.LogError($"MVC files not found: {fullPathMVCForearm}, {fullPathMVCArm}");
                return false;
            }

            // Load MVC values for forearm and arm
            float[] mvcForearmValues = LoadMVCValuesFromFile(fullPathMVCForearm);
            float[] mvcArmValues = LoadMVCValuesFromFile(fullPathMVCArm);
            if (mvcForearmValues == null || mvcArmValues == null)
            {
                Debug.LogError("Failed to load MVC values.");
                return false;
            }

            // Combine forearm and arm MVC values into a single array
            float[] mvcValues = new float[16];
            Array.Copy(mvcForearmValues, 0, mvcValues, 0, 8); // Forearm values
            Array.Copy(mvcArmValues, 0, mvcValues, 8, 8);     // Arm values


            string[] lines = File.ReadAllLines(fileName);

            // 슬라이딩 윈도우를 위한 버퍼

            List<float>[] rectifiedBuffers = new List<float>[16];
            for (int i = 0; i < 16; i++)
            {
                rectifiedBuffers[i] = new List<float>();
            }

            // 필터 초기화
            bandpassFilters = new ButterworthFilter[16];
            for (int i = 0; i < 16; i++)
            {
                bandpassFilters[i] = new ButterworthFilter();
            }
            bool filtersPrimed = false;
            int effectiveWindowSize = windowSize > 0 ? windowSize : 10;
            if (windowSize <= 0)
                Debug.LogWarning("Replay RMS window was unset; using the study default of 10 frames.");

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;

                string[] parts = lines[i].Split(',');
                Quaternion[] rotations = new Quaternion[21];
				Vector3[] positions = new Vector3[21];
                Vector3[] globalpositions = new Vector3[21];
                float[] muscle = new float[16];
                float[] filteredMuscle = new float[16];
                float[] rectifiedMuscle = new float[16];
                float[] rmsMuscle = new float[16];


                for (int j = 0; j < 21; j += 1)
                {
					Vector3 givenLocalPosition = new Vector3(
						-float.Parse(parts[3 * j + 79]),
                        float.Parse(parts[3 * j + 1 + 79]),
                        float.Parse(parts[3 * j + 2 + 79])
                    );

                    Quaternion givenQuaternion = new Quaternion(
                        float.Parse(parts[4 * j + 1 + 142]),
                        -float.Parse(parts[4 * j + 2 + 142]),
                        -float.Parse(parts[4 * j + 3 + 142]),
                        float.Parse(parts[4 * j + 142])
                    );

                    Vector3 givenGlobalPosition = new Vector3(
						-float.Parse(parts[3 * j + 79]),
                        float.Parse(parts[3 * j + 1 + 79]),
                        float.Parse(parts[3 * j + 2 + 79])
                    );


					positions[j] = givenLocalPosition;
                    globalpositions[j] = givenGlobalPosition;
                    rotations[j] = givenQuaternion;
                }

                // Parse muscle data
                try
                {
                    for (int j = 0; j < 16; j += 1)
                    {
                        // Parse raw muscle data
                        muscle[j] = float.TryParse(parts[j], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedX) ? parsedX : 0f;

                        // The clip begins mid-recording. Priming avoids plotting a
                        // zero-state filter reset as an activation burst.
                        if (!filtersPrimed)
                        {
                            bandpassFilters[j].Prime(muscle[j]);
                        }

                        // Butterworth Filtering
                        filteredMuscle[j] = bandpassFilters[j].Apply(muscle[j]);

                        // Rectification
                        rectifiedMuscle[j] = Mathf.Abs(filteredMuscle[j]);
                        rectifiedBuffers[j].Add(rectifiedMuscle[j]);
                        if (rectifiedBuffers[j].Count > effectiveWindowSize)
                        {
                            rectifiedBuffers[j].RemoveAt(0);
                        }

                        // RMS Calculation
                        if (rectifiedBuffers[j].Count == effectiveWindowSize)
                        {
                            rmsMuscle[j] = Mathf.Sqrt(rectifiedBuffers[j].Sum(x => x * x) / effectiveWindowSize);
                        }
                        else
                        {
                            rmsMuscle[j] = 0f; // Insufficient data
                        }

                        // MVC Normalization
                        rmsMuscle[j] = NormalizeRmsByMvc(rmsMuscle[j], mvcValues[j]);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing EMG data at frame {i}. Exception: {ex.Message}");
                    muscle = new float[16]; // Default to zero values if parsing fails
                }

                jointPositionData_Expert[i] = positions;
                jointRotationData_Expert[i] = rotations;
                jointGlobalPositionData_Expert[i] = globalpositions;
                ArmMuscle_Expert[i] = rmsMuscle;
                filtersPrimed = true;
            }

            return true;
        }

        private bool ReadCSV_User(string fileName, string mvcforearm, string mvcarm, Dictionary<int, Quaternion[]> jointRotations, Dictionary<int, Vector3[]> jointPositions)
        {

            string fullPath = Path.Combine(folderPath, fileName);
            string fullPathMVCForearm = Path.Combine(folderPath, mvcforearm);
            string fullPathMVCArm = Path.Combine(folderPath, mvcarm);


            if (!File.Exists(fullPath))
            {
                Debug.LogError($"File not found: {fullPath}");
                return false;
            }

            if (!File.Exists(fullPathMVCForearm) || !File.Exists(fullPathMVCArm))
            {
                Debug.LogError($"MVC files not found: {fullPathMVCForearm}, {fullPathMVCArm}");
                return false;
            }

            // Load MVC values for forearm and arm
            float[] mvcForearmValues = LoadMVCValuesFromFile(fullPathMVCForearm);
            float[] mvcArmValues = LoadMVCValuesFromFile(fullPathMVCArm);
            if (mvcForearmValues == null || mvcArmValues == null)
            {
                Debug.LogError("Failed to load MVC values.");
                return false;
            }

            // Combine forearm and arm MVC values into a single array
            float[] mvcValues = new float[16];
            Array.Copy(mvcForearmValues, 0, mvcValues, 0, 8); // Forearm values
            Array.Copy(mvcArmValues, 0, mvcValues, 8, 8);     // Arm values

            string[] lines = File.ReadAllLines(fullPath);

            // 슬라이딩 윈도우를 위한 버퍼

            List<float>[] rectifiedBuffers = new List<float>[16];
            for (int i = 0; i < 16; i++)
            {
                rectifiedBuffers[i] = new List<float>();
            }

            // 필터 초기화
            bandpassFilters = new ButterworthFilter[16];
            for (int i = 0; i < 16; i++)
            {
                bandpassFilters[i] = new ButterworthFilter();
            }
            bool filtersPrimed = false;
            int effectiveWindowSize = windowSize > 0 ? windowSize : 10;
            if (windowSize <= 0)
                Debug.LogWarning("Replay RMS window was unset; using the study default of 10 frames.");


            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;

                string[] parts = lines[i].Split(',');
                Quaternion[] rotations = new Quaternion[21];
				Vector3[] positions = new Vector3[21];
                Vector3[] Globalpositions = new Vector3[21];
                float[] muscle = new float[16];
                float[] filteredMuscle = new float[16];
                float[] rectifiedMuscle = new float[16];
                float[] rmsMuscle = new float[16];


                for (int j = 0; j < 21; j += 1)
                {
					Vector3 givenLocalPosition = new Vector3(
						float.Parse(parts[3 * j + 79]),
                        float.Parse(parts[3 * j + 1 + 79]),
                        float.Parse(parts[3 * j + 2 + 79])
                    );

                    Quaternion givenQuaternion = new Quaternion(
                        float.Parse(parts[4 * j + 0 + 142]),
                        float.Parse(parts[4 * j + 1 + 142]),
                        float.Parse(parts[4 * j + 2 + 142]),
                        float.Parse(parts[4 * j + 3 + 142])
                    );

                    Vector3 givenGlobalPosition = new Vector3(
						float.Parse(parts[3 * j + 16]),
                        float.Parse(parts[3 * j + 1 + 16]),
                        float.Parse(parts[3 * j + 2 + 16])
                    );

					positions[j] = givenLocalPosition;
                    Globalpositions[j] = givenGlobalPosition;
                    rotations[j] = givenQuaternion;
                }

                // Parse muscle data
                try
                {
                    for (int j = 0; j < 16; j += 1)
                    {
                        // Parse raw muscle data
                        muscle[j] = float.TryParse(parts[j], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedX) ? parsedX : 0f;

                        // A saved replay fragment is not a zero-to-signal stream.
                        // Prime without adding artificial samples to the RMS buffer.
                        if (!filtersPrimed)
                        {
                            bandpassFilters[j].Prime(muscle[j]);
                        }

                        // Butterworth Filtering
                        filteredMuscle[j] = bandpassFilters[j].Apply(muscle[j]);

                        // Rectification
                        rectifiedMuscle[j] = Mathf.Abs(filteredMuscle[j]);
                        rectifiedBuffers[j].Add(rectifiedMuscle[j]);
                        if (rectifiedBuffers[j].Count > effectiveWindowSize)
                        {
                            rectifiedBuffers[j].RemoveAt(0);
                        }

                        // RMS Calculation
                        if (rectifiedBuffers[j].Count == effectiveWindowSize)
                        {
                            rmsMuscle[j] = Mathf.Sqrt(rectifiedBuffers[j].Sum(x => x * x) / effectiveWindowSize);
                        }
                        else
                        {
                            rmsMuscle[j] = 0f; // Insufficient data
                        }

                        // MVC Normalization
                        rmsMuscle[j] = NormalizeRmsByMvc(rmsMuscle[j], mvcValues[j]);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing EMG data at frame {i}. Exception: {ex.Message}");
                    muscle = new float[16]; // Default to zero values if parsing fails
                }

                jointPositionData_User[i] = positions;
                jointGlobalPositionData_User[i] = Globalpositions;
                jointRotationData_User[i] = rotations;
                ArmMuscle_User[i] = rmsMuscle;
                filtersPrimed = true;
            }

            string[] labels = new string[] { "Forearm Flexion", "Forearm Extension", "Arm Flexion", "Arm Extension" };

            int totalFrames = ArmMuscle_User.Count;
            float threshold = 0.1f;

            Dictionary<string, float> rmsMax = new Dictionary<string, float>();
            Dictionary<string, float> rmsSum = new Dictionary<string, float>();
            Dictionary<string, float> onsetFrame = new Dictionary<string, float>();
            Dictionary<string, float> offsetFrame = new Dictionary<string, float>();
            Dictionary<string, float> durationFrame = new Dictionary<string, float>();;
            Dictionary<string, bool> foundOnset = new Dictionary<string, bool>();
            

            // 초기화
            foreach (string label in labels)
            {
                rmsMax[label] = 0f;
                rmsSum[label] = 0f;
                onsetFrame[label] = -1;
                offsetFrame[label] = -1;
                foundOnset[label] = false;
            }

            // 프레임 루프
            foreach (var frame in ArmMuscle_User)
            {
                int t = frame.Key;
                float[] emg = frame.Value;

                GetMvcNormalizedMuscleActivations(
                    emg,
                    out float forearmFlexionMvc,
                    out float forearmExtensionMvc,
                    out float armFlexionMvc,
                    out float armExtensionMvc);
                float[] groupValues = { forearmFlexionMvc, forearmExtensionMvc, armFlexionMvc, armExtensionMvc };

                for (int i = 0; i < groupValues.Length; i++)
                {
                    string label = labels[i];
                    float val = groupValues[i];

                    rmsMax[label] = Mathf.Max(rmsMax[label], val);
                    rmsSum[label] += val;

                    if (!foundOnset[label] && val > threshold)
                    {
                        onsetFrame[label] = t;
                        foundOnset[label] = true;
                    }
                    else if (foundOnset[label] && offsetFrame[label] == -1 && val < threshold)
                    {
                        offsetFrame[label] = t;
                    }
                }
            }

            List<float> ffList = new List<float>();
            List<float> feList = new List<float>();
            List<float> afList = new List<float>();
            List<float> aeList = new List<float>();

            foreach (var frame in ArmMuscle_User)
            {
                float[] emg = frame.Value;
                GetMvcNormalizedMuscleActivations(
                    emg,
                    out float forearmFlexionMvc,
                    out float forearmExtensionMvc,
                    out float armFlexionMvc,
                    out float armExtensionMvc);

                ffList.Add(forearmFlexionMvc);
                feList.Add(forearmExtensionMvc);
                afList.Add(armFlexionMvc);
                aeList.Add(armExtensionMvc);
            }

            // Coactivation 계산
            float forearmCoactivation = ComputeCoactivationRatio(ffList, feList);
            float armCoactivation = ComputeCoactivationRatio(afList, aeList);
            float crossCoactivation1 = ComputeCoactivationRatio(ffList, aeList); // Forearm Flex vs Arm Ext
            float crossCoactivation2 = ComputeCoactivationRatio(feList, afList); // Forearm Ext vs Arm Flex

            // 결과 출력
            Debug.Log("======= Coactivation Ratio (Min/Max Method) =======");
            Debug.Log($"Forearm Coactivation Ratio: {forearmCoactivation:F4}");
            Debug.Log($"Arm Coactivation Ratio: {armCoactivation:F4}");
            Debug.Log($"Cross Coactivation 1 (FF vs AE): {crossCoactivation1:F4}");
            Debug.Log($"Cross Coactivation 2 (FE vs AF): {crossCoactivation2:F4}");

            // 결과 출력
            Dictionary<string, float> rmsMean = new Dictionary<string, float>();  // ✅ 추가

            foreach (string label in labels)
            {
                float mean = rmsSum[label] / totalFrames;
                rmsMean[label] = mean;  // ✅ Mean 저장
                float onset = onsetFrame[label];
                float offset = offsetFrame[label];
        
                Debug.Log($"--- {label} ---");
                Debug.Log($"RMS Max: {rmsMax[label]:F4}");
                Debug.Log($"RMS Mean: {mean:F4}");
                Debug.Log($"Onset Time: {onset :F4} s");
                Debug.Log($"Offset Time: {offset:F4} s");
            }

            Dictionary<string, float> coactivationDict = new Dictionary<string, float>()
            {
                { "Forearm Coactivation", forearmCoactivation },
                { "Arm Coactivation", armCoactivation },
                { "Cross Coactivation 1", crossCoactivation1 },
                { "Cross Coactivation 2", crossCoactivation2 }
            };

            foreach (string label in labels)
            {
                float onset = onsetFrame[label];
                float offset = offsetFrame[label];
                durationFrame[label] = offset - onset;
            }

            foreach (string label in labels)
            {
                // RMS Max & Mean
                RMS_Max_B.DataSource.SetValue("User", label, rmsMax[label]);
                RMS_Mean_B.DataSource.SetValue("User", label, rmsMean[label]);

                // Onset / Offset / Duration
                OnsetTime_B.DataSource.SetValue("User", label, onsetFrame[label] / 30f);
                OffsetTime_B.DataSource.SetValue("User", label, offsetFrame[label] / 30f);
                Duration_B.DataSource.SetValue("User", label, durationFrame[label] / 30f);

                // RMS Max & Mean
                RMS_Max_H.DataSource.SetValue("User", label, rmsMax[label]);
                RMS_Mean_H.DataSource.SetValue("User", label, rmsMean[label]);

                // Onset / Offset / Duration
                OnsetTime_H.DataSource.SetValue("User", label, onsetFrame[label] / 30f);
                OffsetTime_H.DataSource.SetValue("User", label, offsetFrame[label] / 30f);
                Duration_H.DataSource.SetValue("User", label, durationFrame[label] / 30f);
            }

            // Coactivation은 별도 이름이므로 따로 처리
            foreach (var pair in coactivationDict)
            {
                Coactivation_B.DataSource.SetValue("User", pair.Key, pair.Value);
                Coactivation_H.DataSource.SetValue("User", pair.Key, pair.Value);
            }

            return true;
        }

        private static float NormalizeRmsByMvc(float rms, float mvc)
        {
            // Keep the physiological RMS/MVC ratio intact. A value above 1.0 is
            // possible; capping is deferred to the visual-output boundary.
            return EmgVisualizationMath.NormalizeRmsByMvc(rms, mvc);
        }

        private float[] LoadMVCValuesFromFile(string filePath)
        {
            string resolvedPath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath))
            {
                Debug.LogError($"File not found: {resolvedPath}");
                return null;
            }

            string[] lines = File.ReadAllLines(filePath);
            float[] mvcValues = new float[8];
            int sequentialChannel = 0;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(',');
                string valueText = parts[parts.Length - 1].Trim();

                // First validate the value. This naturally skips the legacy
                // "Channel, MVC Value" header, regardless of locale/spacing.
                if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    continue;

                // Accept both public v2 (one value per line) and legacy
                // "Channel N,value" files. For any nonstandard two-column
                // label, retain a safe sequential fallback rather than
                // silently leaving every calibration channel at zero.
                int channel = sequentialChannel;
                if (parts.Length >= 2)
                {
                    string channelLabel = parts[0].Trim();
                    int firstDigit = channelLabel.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                    if (firstDigit >= 0 &&
                        int.TryParse(channelLabel.Substring(firstDigit), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int oneBasedChannel))
                    {
                        channel = oneBasedChannel - 1;
                    }
                }

                if (channel < 0 || channel >= mvcValues.Length)
                {
                    Debug.LogWarning($"Ignoring out-of-range MVC channel in {filePath}: {rawLine}");
                    continue;
                }

                mvcValues[channel] = value;
                sequentialChannel = Math.Max(sequentialChannel, channel + 1);
            }

            // RMS/MVC cannot be computed from a zero or missing calibration.
            // Treat this as an invalid replay input rather than silently
            // returning an all-zero EMG trace, which is visually misleading.
            if (mvcValues.Any(value => value <= 0f))
            {
                Debug.LogError($"MVC file is incomplete or contains non-positive values: {resolvedPath}. " +
                    $"Parsed values: [{string.Join(", ", mvcValues.Select(value => value.ToString("F6", CultureInfo.InvariantCulture)))}]. " +
                    "Provide eight strictly positive channel calibration values.");
                return null;
            }

            Debug.Log($"Loaded MVC calibration: {resolvedPath}; " +
                $"range {mvcValues.Min():F6}-{mvcValues.Max():F6}.");

            return mvcValues;
        }

        private class ButterworthFilter
        {
            private readonly float[] a; // Denominator coefficients
            private readonly float[] b; // Numerator coefficients
            private readonly float[] x; // Input history
            private readonly float[] y; // Output history

            public ButterworthFilter()
            {
                // 10–20 Hz, 2nd-order Butterworth band-pass at the replay's
                // nominal 50 Hz processing rate (Nyquist = 25 Hz).
                b = new float[] { 0.2066f, 0f, -0.4131f, 0f, 0.2066f };
                a = new float[] { 1f, 0.9051f, 0.5979f, 0.2907f, 0.1958f };

                // Initialize history buffers
                x = new float[b.Length];
                y = new float[a.Length];
            }

            public float Apply(float input)
            {
                // Shift input history
                for (int i = x.Length - 1; i > 0; i--)
                    x[i] = x[i - 1];
                x[0] = input;

                // Compute the filter output
                float output = 0;
                for (int i = 0; i < b.Length; i++)
                    output += b[i] * x[i];
                for (int i = 1; i < a.Length; i++)
                    output -= a[i] * y[i - 1];

                // Shift output history
                for (int i = y.Length - 1; i > 0; i--)
                    y[i] = y[i - 1];
                y[0] = output;

                return output;
            }

            /// <summary>
            /// Drives the causal filter to the steady state of the initial clip
            /// level. This is a replay-display guard; it never enters the RMS data.
            /// </summary>
            public void Prime(float initialInput)
            {
                for (int i = 0; i < 128; i++)
                {
                    Apply(initialInput);
                }
            }
        }

        // Coactivation Ratio 계산 함수
        float ComputeCoactivationRatio(List<float> flexorList, List<float> extensorList)
        {
            float minSum = 0f;
            float maxSum = 0f;

            for (int i = 0; i < flexorList.Count; i++)
            {
                float flexor = flexorList[i];
                float extensor = extensorList[i];

                minSum += Mathf.Min(flexor, extensor);
                maxSum += Mathf.Max(flexor, extensor);
            }

            return (maxSum > 0f) ? (minSum / maxSum) : 0f;
        }

        
        private void Normalization()
        {
            float scaleFactor = scaleControllerInstance != null ? scaleControllerInstance.ScaleValue : 1f;

            for (int i=0; i<jointPositionData_Expert.Count; i++)
            {
                for (int j=0; j < jointPositionData_Expert[i].Length; j++)
                {
                    jointPositionData_Expert[i][j] /= 100;
                    jointPositionData_Expert[i][j] *= scaleFactor;
                }
            }

            for (int i=0; i<jointPositionData_User.Count; i++)
            {
                for (int j=0; j < jointPositionData_User[i].Length; j++)
                {
                    jointPositionData_User[i][j] *= scaleFactor;
                }
            }

        }

        public void StartMotion(string participant1, string participant2, string feedbacktype)
        {
            // 참가자 ID 유효성 검사
            if (string.IsNullOrEmpty(participant1))
            {
                Debug.LogError("Participant 1 is missing. Cannot start motion.");
                return;
            }

            if (string.IsNullOrEmpty(participant2))
            {
                Debug.LogError("Participant 2 is missing. Cannot start motion.");
                return;
            }

            if (boundTransforms && motionUpdateMethod == UpdateMethod.Normal)
            {
                if (physicalReference_Expert.Initiated())
                {
                    ReleasePhysicalContext();
                }

                if (physicalReference_User.Initiated())
                {
                    ReleasePhysicalContext();
                }

                Debug.Log("StartMotion function will launch after countdown.");
                StartCoroutine(CountdownAndStart(participant1, participant2, feedbacktype));
            }
        }

         private IEnumerator CountdownAndStart(string participant1, string participant2, string feedbacktype)
        {
            if (startButton != null) startButton.enabled = false; // 버튼 비활성화
            if (countdownText != null) countdownText.gameObject.SetActive(true);

            // 카운트다운 및 TTS 재생 (5, 4, 3, 2, 1, Go!)
            for (int i = 0; i < countdownClips.Length; i++)
            {
                if (countdownText != null) countdownText.text = (3 - i > 0) ? (3 - i).ToString() : "Go!";

                if (ttsAudioSource != null && countdownClips[i] != null)
                {
                    ttsAudioSource.PlayOneShot(countdownClips[i]); // 오디오 재생
                }

                yield return new WaitForSeconds(1f); // 1초 대기
            }

            if (countdownText != null) countdownText.gameObject.SetActive(false); // UI 숨기기
            StartCoroutine(DelayedStartMotion(participant1, participant2, feedbacktype)); // Motion 시작

            if (startButton != null) startButton.enabled = true; // 버튼 다시 활성화
        }

        private IEnumerator DelayedStartMotion(string participant1, string participant2, string feedbacktype)
        {
            // 5초 대기
            yield return new WaitForSeconds(0f);

            Debug.Log("StartMotion function has been launched after 5 seconds.");

            if (feedbacktype == "replay")
            {
                // 코루틴 실행: Motion 적용 및 진동 데이터 전송
                StartCoroutine(StartOnlyMotion(participant1, participant2));
            }
        }

        private float ComputeJointAngle(Vector3 jointA, Vector3 jointB, Vector3 jointC)
        {
            Vector3 vec1 = jointA - jointB; // A -> B 벡터
            Vector3 vec2 = jointC - jointB; // C -> B 벡터

            float angle = Vector3.Angle(vec1, vec2); // 두 벡터 사이의 각도 계산
            return angle;
        }

        private IEnumerator StartOnlyMotion(string participant1, string participant2)
        {
            bool isFirstExpertFrame = true;
            bool isFirstUserFrame = true;

            List<ReplayFramePair> alignedFrames = BuildDtwFramePairs();
            if (alignedFrames.Count == 0)
            {
                Debug.LogError("Replay did not start because DTW produced no frame pairs.");
                isReplaying = false;
                OnReplayFinished?.Invoke();
                yield break;
            }

            Debug.Log($"DTW replay alignment: {alignedFrames.Count} synchronized pairs " +
                $"(expert {jointPositionData_Expert.Count} frames, user {jointPositionData_User.Count} frames).");

            // A trace starts empty and receives one synchronized EMG point per DTW pair.
            // Do not pre-populate it with zeroes at the same x-coordinates.
            PrepareReplayMuscleGraphs(alignedFrames.Count);

            Vector3 firstHipPositionExpert = Vector3.zero;
            Vector3 firstHipPositionUser = Vector3.zero;

            float forearmFlexionEMAExpert = 0f;
            float forearmExtensionEMAExpert = 0f;
            float armFlexionEMAExpert = 0f;
            float armExtensionEMAExpert = 0f;

            float forearmFlexionEMAUser = 0f;
            float forearmExtensionEMAUser = 0f;
            float armFlexionEMAUser = 0f;
            float armExtensionEMAUser = 0f;

            float nextFrameTime = Time.time;
            int pairIndex = 0;
            while (pairIndex < alignedFrames.Count)
            {
                if (Time.time < nextFrameTime)
                {
                    yield return null;
                    continue;
                }

                ReplayFramePair pair = alignedFrames[pairIndex];
                ApplyFrameToTransform(
                    "expert", pairIndex, transforms_expert,
                    jointRotationData_Expert[pair.ExpertFrame], jointPositionData_Expert[pair.ExpertFrame], ArmMuscle_Expert[pair.ExpertFrame],
                    bonePositionOffsets_Expert, boneRotationOffsets_Expert, orignalRot_Expert, orignalParentRot_Expert,
                    ref firstHipPositionExpert, isFirstExpertFrame, enableHipMove, disableBoneMovement_Expert,
                    ref forearmFlexionEMAExpert, ref forearmExtensionEMAExpert, ref armFlexionEMAExpert, ref armExtensionEMAExpert,
                    alpha, threshold, orignalPositions_Expert);
                isFirstExpertFrame = false;

                ApplyFrameToTransform(
                    "user", pairIndex, transforms_user,
                    jointRotationData_User[pair.UserFrame], jointPositionData_User[pair.UserFrame], ArmMuscle_User[pair.UserFrame],
                    bonePositionOffsets_User, boneRotationOffsets_User, orignalRot_User, orignalParentRot_User,
                    ref firstHipPositionUser, isFirstUserFrame, enableHipMove, disableBoneMovement_User,
                    ref forearmFlexionEMAUser, ref forearmExtensionEMAUser, ref armFlexionEMAUser, ref armExtensionEMAUser,
                    alpha, threshold, orignalPositions_User);
                isFirstUserFrame = false;

                nextFrameTime = Time.time + (1f / (30f * speedScaler));
                pairIndex++;
                yield return null;
            }

            // 모션 재생 끝남
            isReplaying = false;
            OnReplayFinished?.Invoke();
            isFirstExpertFrame = false;
            isFirstUserFrame = false;
        }
        


        private struct ReplayFramePair
        {
            public readonly int ExpertFrame;
            public readonly int UserFrame;

            public ReplayFramePair(int expertFrame, int userFrame)
            {
                ExpertFrame = expertFrame;
                UserFrame = userFrame;
            }
        }

        /// <summary>
        /// Aligns expert and user poses through dynamic time warping over
        /// scale-normalized movement from each recording's initial posture. The
        /// striking-side arm contributes 80% of the cost and Spine2 contributes
        /// 20%. Longer user recordings are first cropped around their detected
        /// right-arm movement, avoiding alignment to idle frames.
        /// </summary>
        private List<ReplayFramePair> BuildDtwFramePairs()
        {
            List<int> expertFrames = jointPositionData_Expert.Keys.OrderBy(frame => frame).ToList();
            List<int> userFrames = jointPositionData_User.Keys.OrderBy(frame => frame).ToList();
            if (expertFrames.Count == 0 || userFrames.Count == 0)
            {
                return new List<ReplayFramePair>();
            }

            userFrames = SelectActiveUserFrameWindow(userFrames, expertFrames.Count);
            Dictionary<int, Vector3[]> expertDisplacements = BuildNormalizedRootRelativeDisplacements(jointPositionData_Expert, expertFrames);
            Dictionary<int, Vector3[]> userDisplacements = BuildNormalizedRootRelativeDisplacements(jointPositionData_User, userFrames);

            int expertCount = expertFrames.Count;
            int userCount = userFrames.Count;
            float[,] accumulatedCost = new float[expertCount, userCount];
            byte[,] previousStep = new byte[expertCount, userCount]; // 0=diagonal, 1=up, 2=left

            for (int expertIndex = 0; expertIndex < expertCount; expertIndex++)
            {
                for (int userIndex = 0; userIndex < userCount; userIndex++)
                {
                    float poseCost = ComputeTaskFocusedDisplacementCost(
                        expertDisplacements[expertFrames[expertIndex]],
                        userDisplacements[userFrames[userIndex]]);

                    if (expertIndex == 0 && userIndex == 0)
                    {
                        accumulatedCost[expertIndex, userIndex] = poseCost;
                        continue;
                    }

                    float bestPreviousCost = float.PositiveInfinity;
                    byte bestStep = 0;
                    if (expertIndex > 0 && userIndex > 0 && accumulatedCost[expertIndex - 1, userIndex - 1] < bestPreviousCost)
                    {
                        bestPreviousCost = accumulatedCost[expertIndex - 1, userIndex - 1];
                        bestStep = 0;
                    }
                    if (expertIndex > 0 && accumulatedCost[expertIndex - 1, userIndex] < bestPreviousCost)
                    {
                        bestPreviousCost = accumulatedCost[expertIndex - 1, userIndex];
                        bestStep = 1;
                    }
                    if (userIndex > 0 && accumulatedCost[expertIndex, userIndex - 1] < bestPreviousCost)
                    {
                        bestPreviousCost = accumulatedCost[expertIndex, userIndex - 1];
                        bestStep = 2;
                    }

                    accumulatedCost[expertIndex, userIndex] = poseCost + bestPreviousCost;
                    previousStep[expertIndex, userIndex] = bestStep;
                }
            }

            List<ReplayFramePair> path = new List<ReplayFramePair>();
            int expertCursor = expertCount - 1;
            int userCursor = userCount - 1;
            path.Add(new ReplayFramePair(expertFrames[expertCursor], userFrames[userCursor]));
            while (expertCursor > 0 || userCursor > 0)
            {
                byte step = previousStep[expertCursor, userCursor];
                if (step == 0 && expertCursor > 0 && userCursor > 0)
                {
                    expertCursor--;
                    userCursor--;
                }
                else if (step == 1 && expertCursor > 0)
                {
                    expertCursor--;
                }
                else if (userCursor > 0)
                {
                    userCursor--;
                }
                else
                {
                    break;
                }

                path.Add(new ReplayFramePair(expertFrames[expertCursor], userFrames[userCursor]));
            }

            path.Reverse();
            return path;
        }

        private List<int> SelectActiveUserFrameWindow(List<int> userFrames, int targetFrameCount)
        {
            if (userFrames.Count <= targetFrameCount)
            {
                return userFrames;
            }

            float[] movementEnergy = new float[userFrames.Count];
            int[] armJoints = {
                (int)NeuronBones_simple.RightShoulder,
                (int)NeuronBones_simple.RightArm,
                (int)NeuronBones_simple.RightForeArm,
                (int)NeuronBones_simple.RightHand
            };
            for (int index = 0; index < userFrames.Count; index++)
            {
                Quaternion[] previous = jointRotationData_User[userFrames[Mathf.Max(0, index - 1)]];
                Quaternion[] following = jointRotationData_User[userFrames[Mathf.Min(userFrames.Count - 1, index + 1)]];
                foreach (int joint in armJoints)
                {
                    movementEnergy[index] += 0.5f * Quaternion.Angle(previous[joint], following[joint]);
                }
            }

            float peakEnergy = movementEnergy.Max();
            if (peakEnergy <= Mathf.Epsilon)
            {
                return userFrames;
            }

            float threshold = peakEnergy * 0.25f;
            int firstActive = System.Array.FindIndex(movementEnergy, energy => energy >= threshold);
            int lastActive = System.Array.FindLastIndex(movementEnergy, energy => energy >= threshold);
            int centre = (firstActive + lastActive) / 2;
            int start = Mathf.Clamp(centre - targetFrameCount / 2, 0, userFrames.Count - targetFrameCount);
            Debug.Log($"DTW selected active user window: frames {userFrames[start]}-{userFrames[start + targetFrameCount - 1]} of {userFrames.Count} recorded frames.");
            return userFrames.GetRange(start, targetFrameCount);
        }

        private static Dictionary<int, Vector3[]> BuildNormalizedRootRelativeDisplacements(
            Dictionary<int, Vector3[]> poses,
            List<int> frames)
        {
            int jointCount = (int)NeuronBones_simple.NumOfBones;
            int baselineCount = Mathf.Min(10, frames.Count);
            Vector3[] baseline = new Vector3[jointCount];
            for (int index = 0; index < baselineCount; index++)
            {
                Vector3[] pose = poses[frames[index]];
                Vector3 root = pose[(int)NeuronBones_simple.Hips];
                for (int joint = 0; joint < jointCount; joint++)
                {
                    baseline[joint] += pose[joint] - root;
                }
            }
            for (int joint = 0; joint < jointCount; joint++)
            {
                baseline[joint] /= baselineCount;
            }

            List<float> armLengths = new List<float>();
            foreach (int frame in frames)
            {
                Vector3[] pose = poses[frame];
                armLengths.Add(Vector3.Distance(
                    pose[(int)NeuronBones_simple.RightHand],
                    pose[(int)NeuronBones_simple.RightShoulder]));
            }
            armLengths.Sort();
            float scale = armLengths[armLengths.Count / 2];
            scale = Mathf.Max(scale, 0.0001f);

            Dictionary<int, Vector3[]> displacements = new Dictionary<int, Vector3[]>();
            foreach (int frame in frames)
            {
                Vector3[] pose = poses[frame];
                Vector3 root = pose[(int)NeuronBones_simple.Hips];
                Vector3[] displacement = new Vector3[jointCount];
                for (int joint = 0; joint < jointCount; joint++)
                {
                    displacement[joint] = ((pose[joint] - root) - baseline[joint]) / scale;
                }
                displacements[frame] = displacement;
            }
            return displacements;
        }

        private static float ComputeTaskFocusedDisplacementCost(Vector3[] expertPose, Vector3[] userPose)
        {
            int jointCount = Mathf.Min(expertPose.Length, userPose.Length);
            int[] weightedJoints = {
                (int)NeuronBones_simple.Spine2,
                (int)NeuronBones_simple.RightShoulder,
                (int)NeuronBones_simple.RightArm,
                (int)NeuronBones_simple.RightForeArm,
                (int)NeuronBones_simple.RightHand
            };
            float weightedDistance = 0f;
            float availableWeight = 0f;
            const float jointWeight = 0.20f;

            foreach (int joint in weightedJoints)
            {
                if (joint >= jointCount)
                {
                    continue;
                }
                weightedDistance += jointWeight * Vector3.Distance(expertPose[joint], userPose[joint]);
                availableWeight += jointWeight;
            }

            return availableWeight > 0f
                ? weightedDistance / availableWeight
                : float.PositiveInfinity;
        }

        private void ApplyFrameToTransform(
            string user,
            int frameIdx,
            Transform[] transforms,
            Quaternion[] currentRotation,
            Vector3[] currentPosition,
            float[] ArmMuscle,
            Vector3[] positionOffsets,
            Vector3[] rotationOffsets,
            Quaternion[] orignalRot,
            Quaternion[] orignalParentRot,
            ref Vector3 firstHipPosition,
            bool isFirstFrame,
            bool enableHipMove,
            bool[] disableBoneMovement,
            ref float forearmFlexionEMA,
            ref float forearmExtensionEMA,
            ref float armFlexionEMA,
            ref float armExtensionEMA,
            float alpha,
            float threshold,
            Vector3[] orignalPositions // 추가
        )
        {
            // Normalize Hip Position
            if (isFirstFrame) // 첫 프레임에서만 실행
            {
                // 첫 프레임의 Hip 위치를 저장
                firstHipPosition = currentPosition[(int)NeuronBones_simple.Hips];
                SetPosition(transforms, NeuronBones_simple.Hips, Vector3.zero); // Hip 위치를 (0, 0, 0)으로 고정
                isFirstFrame = false; // 플래그를 false로 설정하여 이후 프레임에서는 실행하지 않음
            }
            else if (enableHipMove && (!disableBoneMovement[(int)NeuronBones_simple.Hips]))
            {
                    // 모든 프레임에서 첫 프레임의 Hip 위치를 뺀 상대 좌표 적용
                Vector3 relativePosition = currentPosition[(int)NeuronBones_simple.Hips] - firstHipPosition;
                SetPosition(transforms, NeuronBones_simple.Hips, relativePosition);
            }
            else
            {
                // Y 축만 조정하며 첫 프레임의 Hip 위치를 빼서 적용
                Vector3 p = currentPosition[(int)NeuronBones_simple.Hips] - firstHipPosition;
                SetPosition(transforms, NeuronBones_simple.Hips, new Vector3(0f, p.y, 0f));
            }

            SetRotation(transforms, NeuronBones_simple.Hips,
                (Quaternion.Euler(currentRotation[(int)NeuronBones_simple.Hips].eulerAngles) * orignalRot[(int)NeuronBones_simple.Hips]).eulerAngles);

            // apply positions
            for (int i = 1; i < (int)NeuronBones_simple.NumOfBones && i < transforms.Length; ++i)
            {
                if (transforms[i] == null)
                    continue;

                // q
                Quaternion orignalBoneRot = Quaternion.identity;
                if (orignalRot != null)
                {
                    orignalBoneRot = orignalRot[i];
                }
                Vector3 rot = currentRotation[i].eulerAngles + rotationOffsets[i];
                //Debug.LogError(actor.AvatarIndex + " " +  actor.GetReceivedRotation((NeuronBones)i) + ", " + rot);
                Quaternion srcQ = Quaternion.Euler(rot);

                Quaternion usedQ = Quaternion.Inverse(orignalParentRot[i]) * srcQ * orignalParentRot[i];
                Vector3 transedRot = usedQ.eulerAngles;
                Quaternion finalBoneQ = Quaternion.Euler(transedRot) * orignalBoneRot;
                SetRotation(transforms, (NeuronBones_simple)i, finalBoneQ.eulerAngles);

                // p   
                bool enableNodeMove = (currentPosition[i] != null);
                enableNodeMove &= (!disableBoneMovement[i]);
                /*if (!enableFingerMove)
                {
                    if (i >= (int)NeuronBones_simple.RightHand && i <= (int)NeuronBones_simple.RightHandPinky3)
                        enableNodeMove = false;
                    if (i >= (int)NeuronBones.LeftHand && i <= (int)NeuronBones.LeftHandPinky3)
                        enableNodeMove = false;
                }*/
                if (enableNodeMove)
                {
                    Vector3 srcP = currentPosition[i] + positionOffsets[i];
                    Vector3 finalP = Quaternion.Inverse(orignalParentRot[i]) * srcP;
                    SetPosition(transforms, (NeuronBones_simple)i, finalP);
                }
                else
                {
                    SetPosition(transforms, (NeuronBones_simple)i, orignalPositions[i], true);
                }
                //  SetPosition( transforms, (NeuronBones)i, actor.GetReceivedPosition( (NeuronBones)i ) + bonePositionOffsets[i] );
                //	SetRotation( transforms, (NeuronBones)i, actor.GetReceivedRotation( (NeuronBones)i ) + boneRotationOffsets[i] );
            }
            GetStrokeMuscleActivations(
                ArmMuscle,
                out float normForearmFlexion,
                out float normForearmExtension,
                out float normArmFlexion,
                out float normArmExtension);

            ForearmFlexionMuscle.DataSource.AddPointToCategory(user, frameIdx * xStep, normForearmFlexion); // ✅ 왼쪽부터 순서대로 채움
            ForearmExtensionMuscle.DataSource.AddPointToCategory(user, frameIdx * xStep, normForearmExtension); // ✅ 왼쪽부터 순서대로 채움
            ArmFlexionMuscle.DataSource.AddPointToCategory(user, frameIdx * xStep, normArmFlexion); // ✅ 왼쪽부터 순서대로 채움
            ArmExtensionMuscle.DataSource.AddPointToCategory(user, frameIdx * xStep, normArmExtension); // ✅ 왼쪽부터 순서대로 채움

        }



		public Transform[][] GetTransforms()
        {
            return new Transform[][] { transforms_expert, transforms_user };
        }
        

        
        static bool ValidateVector3( Vector3 vec )
		{
			return !float.IsNaN( vec.x ) && !float.IsNaN( vec.y ) && !float.IsNaN( vec.z )
				&& !float.IsInfinity( vec.x ) && !float.IsInfinity( vec.y ) && !float.IsInfinity( vec.z );
		}
		
		// set position for bone
		static void SetPosition( Transform[] transforms, NeuronBones_simple bone, Vector3 pos, bool withOriginlPosition = false )
		{
			Transform t = transforms[(int)bone];
			if( t != null )
			{
                if (!withOriginlPosition)
                {
                    // calculate position when we have scale
                    Vector3 lossyScale = t.parent == null ? Vector3.one : t.parent.lossyScale;

                    pos.Scale(new Vector3(1.0f / lossyScale.x, 1.0f / lossyScale.y, 1.0f / lossyScale.z));
                }

                if ( !float.IsNaN( pos.x ) && !float.IsNaN( pos.y ) && !float.IsNaN( pos.z ) )
				{
					t.localPosition = pos;
				}
			}
		}
		
		// set rotation for bone
		static void SetRotation( Transform[] transforms, NeuronBones_simple bone, Vector3 rotation )
		{
			Transform t = transforms[(int)bone];
			if( t != null )
			{
				Quaternion rot = Quaternion.Euler( rotation );
				if( !float.IsNaN( rot.x ) && !float.IsNaN( rot.y ) && !float.IsNaN( rot.z ) && !float.IsNaN( rot.w ) )
				{
					t.localRotation = rot;
				}
			}
		}

		// apply Transforms of src bones to dest Rigidbody Components of bone
		public void ApplyMotionPhysically( Transform[] src, Transform[] dest )
		{
			if( src != null && dest != null )
			{
				for( int i = 0; i < (int)NeuronBones.NumOfBones; ++i )
				{
					Transform src_transform = src[i];
					Transform dest_transform = dest[i];
					if( src_transform != null && dest_transform != null )
					{
						Rigidbody rigidbody = dest_transform.GetComponent<Rigidbody>();
						if( rigidbody != null )
						{
							switch (motionUpdateMethod) {
							case UpdateMethod.Physical:
								rigidbody.MovePosition( src_transform.position );
								rigidbody.MoveRotation( src_transform.rotation );
								break;

							case UpdateMethod.EstimatedPhysical:
								Quaternion dAng = src_transform.rotation * Quaternion.Inverse (dest_transform.rotation);
								float angle = 0.0f;
								Vector3 axis = Vector3.zero;
								dAng.ToAngleAxis (out angle, out axis);

                                if (angle > 180f)
                                    angle -= 360f;

                                // Here I drop down to 0.9f times the desired movement,
                                // since we'd rather undershoot and ease into the correct angle
                                // than overshoot and oscillate around it in the event of errors.
                                Vector3 angular = (0.9f * Mathf.Deg2Rad * angle / Time.fixedDeltaTime) * axis.normalized;


                                Vector3 velocityTarget = (src_transform.position - dest_transform.position) / Time.fixedDeltaTime;
                                Vector3 angularTarget = angular;

                                ApplyVelocity(rigidbody, velocityTarget, angularTarget);

								break;

							case UpdateMethod.MixedPhysical:
								Vector3 velocityTarget2 = (src_transform.position - dest_transform.position) / Time.fixedDeltaTime;

								Vector3 v = Vector3.MoveTowards(rigidbody.velocity, velocityTarget2, 10.0f);
								if( ValidateVector3( v ) )
								{
									rigidbody.velocity = v;
								}

								rigidbody.MoveRotation( src_transform.rotation );

								break;
							}
						}
					}
				}
			}
		}

        /// <summary>
        /// Aggregates the four functional groups from the 16-channel replay
        /// record without applying a display cap. Indices 0–7 are forearm;
        /// 8–15 are arm. Within each device, 0,1,6,7 are flexion and 2,3,4,5
        /// are extension.
        /// </summary>
        private static void GetMvcNormalizedMuscleActivations(
            float[] emg,
            out float forearmFlexion,
            out float forearmExtension,
            out float armFlexion,
            out float armExtension)
        {
            float[] forearmChannels = new float[8];
            float[] armChannels = new float[8];
            for (int localChannel = 0; localChannel < 8; localChannel++)
            {
                forearmChannels[localChannel] = localChannel < emg.Length ? emg[localChannel] : 0f;
                int armIndex = localChannel + 8;
                armChannels[localChannel] = armIndex < emg.Length ? emg[armIndex] : 0f;
            }

            float forearmFlexionMvc = EmgVisualizationMath.ComputeGroupRms(forearmChannels, EmgVisualizationMath.FlexionChannels);
            float forearmExtensionMvc = EmgVisualizationMath.ComputeGroupRms(forearmChannels, EmgVisualizationMath.ExtensionChannels);
            float armFlexionMvc = EmgVisualizationMath.ComputeGroupRms(armChannels, EmgVisualizationMath.FlexionChannels);
            float armExtensionMvc = EmgVisualizationMath.ComputeGroupRms(armChannels, EmgVisualizationMath.ExtensionChannels);

            forearmFlexion = forearmFlexionMvc;
            forearmExtension = forearmExtensionMvc;
            armFlexion = armFlexionMvc;
            armExtension = armExtensionMvc;
        }

        /// <summary>
        /// Produces the chart values. MVC normalization and display capping are
        /// intentionally separate: 1.5 denotes the top of the HUD scale, not
        /// proof that the underlying RMS/MVC value cannot exceed 1.0.
        /// </summary>
        private static void GetStrokeMuscleActivations(
            float[] emg,
            out float forearmFlexion,
            out float forearmExtension,
            out float armFlexion,
            out float armExtension)
        {
            GetMvcNormalizedMuscleActivations(
                emg,
                out float forearmFlexionMvc,
                out float forearmExtensionMvc,
                out float armFlexionMvc,
                out float armExtensionMvc);

            // The 1.5 ceiling is a display range, not a second normalization.
            forearmFlexion = EmgVisualizationMath.CapForDisplay(forearmFlexionMvc);
            forearmExtension = EmgVisualizationMath.CapForDisplay(forearmExtensionMvc);
            armFlexion = EmgVisualizationMath.CapForDisplay(armFlexionMvc);
            armExtension = EmgVisualizationMath.CapForDisplay(armExtensionMvc);
        }


		void ApplyVelocity(Rigidbody rb, Vector3 velocityTarget, Vector3 angularTarget)
		{
            Vector3 v =  Vector3.MoveTowards(rb.velocity, velocityTarget, 100.0f);
			if( ValidateVector3( v ) )
			{

                rb.velocity = v;
			}

            v =  Vector3.MoveTowards(rb.angularVelocity, angularTarget, 100.0f);
			if( ValidateVector3( v ) )
			{

                rb.angularVelocity = v;

			}
		}

		
		public bool BindExpert( Transform root_expert, string prefix_expert )
		{
			this.root_expert = root_expert;
			this.prefix_expert = prefix_expert;
			//int bound_count = 
            NeuronHelper.Bind( root_expert, transforms_expert, prefix_expert, false, (skeletonType == NeuronEnums.SkeletonType.PerceptionNeuronStudio) ? NeuronBoneVersion.V1 : NeuronBoneVersion.V2);
            boundTransforms = true; // bound_count >= (int)NeuronBones.NumOfBones;
			UpdateOffset();
            CaluateOrignalRot();
            return boundTransforms;
		}

        public bool BindUser( Transform root, string prefix )
		{
            this.root_user = root;
            this.prefix_user = prefix;
			//int bound_count = 
            NeuronHelper.Bind( root, transforms_user, prefix, false, (skeletonType == NeuronEnums.SkeletonType.PerceptionNeuronStudio) ? NeuronBoneVersion.V1 : NeuronBoneVersion.V2);
            boundTransforms = true; // bound_count >= (int)NeuronBones.NumOfBones;
			UpdateOffset();
            CaluateOrignalRot();
            return boundTransforms;
		}
		
		void InitPhysicalContext()
		{
			if( physicalReference_Expert.Init( root_expert, prefix_expert, transforms_expert, physicalReferenceOverride ) )
			{
				// break original object's hierachy of transforms, so we can use MovePosition() and MoveRotation() to set transform
				NeuronHelper.BreakHierarchy( transforms_expert );
			}

            if( physicalReference_User.Init( root_user, prefix_user, transforms_user, physicalReferenceOverride ) )
			{
				// break original object's hierachy of transforms, so we can use MovePosition() and MoveRotation() to set transform
				NeuronHelper.BreakHierarchy( transforms_user );
			}

			CheckRigidbodySettings ();
		}

		
		void ReleasePhysicalContext()
		{
			physicalReference_Expert.Release();
            physicalReference_User.Release();
		}
		
		void UpdateOffset()
		{
			// initiate values
			for( int i = 0; i < (int)NeuronBones_simple.NumOfBones; ++i )
			{
				bonePositionOffsets_Expert[i] = Vector3.zero;
				boneRotationOffsets_Expert[i] = Vector3.zero;
                bonePositionOffsets_User[i] = Vector3.zero;
				boneRotationOffsets_User[i] = Vector3.zero;
            }
		}

        void CaluateOrignalRot()
        {
            for (int i = 0; i < orignalPositions_Expert.Length; i++)
            {
                orignalPositions_Expert[i] = transforms_expert[i] == null ? Vector3.zero : transforms_expert[i].localPosition;
            }
            for (int i = 0; i < orignalPositions_User.Length; i++)
            {
                orignalPositions_User[i] = transforms_user[i] == null ? Vector3.zero : transforms_user[i].localPosition;
            }
            for (int i = 0; i < orignalRot_Expert.Length; i++)
            {
                orignalRot_Expert[i] = transforms_expert[i] == null ? Quaternion.identity : transforms_expert[i].localRotation;
            }
            for (int i = 0; i < orignalRot_User.Length; i++)
            {
                orignalRot_User[i] = transforms_user[i] == null ? Quaternion.identity : transforms_user[i].localRotation;
            }
            for (int i = 0; i < orignalRot_Expert.Length; i++)
            {
                Quaternion parentQs = Quaternion.identity;
                if (transforms_expert[i] == null)
                {
                    orignalParentRot_Expert[i] = Quaternion.identity;
                    continue;
                }
                Transform tempParent = transforms_expert[i].transform.parent;
                while (tempParent != null)
                {
                    parentQs = tempParent.localRotation * parentQs;
                    tempParent = tempParent.parent;

                    // 배열의 첫 번째 Transform과 비교
                    if (tempParent == null || (transforms_expert.Length > 0 && tempParent == transforms_expert[0]))
                        break;
                }
                orignalParentRot_Expert[i] = parentQs;
            }

            for (int i = 0; i < orignalRot_User.Length; i++)
            {
                Quaternion parentQs = Quaternion.identity;
                if (transforms_user[i] == null)
                {
                    orignalParentRot_User[i] = Quaternion.identity;
                    continue;
                }
                Transform tempParent = transforms_user[i].transform.parent;
                while (tempParent != null)
                {
                    parentQs = tempParent.localRotation * parentQs;
                    tempParent = tempParent.parent;

                    // 배열의 첫 번째 Transform과 비교
                    if (tempParent == null || (transforms_user.Length > 0 && tempParent == transforms_user[0]))
                        break;
                }
                orignalParentRot_User[i] = parentQs;
            }

        }
		void CheckRigidbodySettings( ){
			//check if rigidbodies have correct settings
			bool kinematicSetting = false;
			if (motionUpdateMethod == UpdateMethod.Physical) {
				kinematicSetting = true;
			}

			for( int i = 0; i < (int)NeuronBones_simple.NumOfBones && i < transforms_expert.Length; ++i )
			{
				Rigidbody r = transforms_expert[i].GetComponent<Rigidbody> ();
				if (r != null) {
					r.isKinematic = kinematicSetting;
				}
			}

            for( int i = 0; i < (int)NeuronBones_simple.NumOfBones && i < transforms_user.Length; ++i )
			{
				Rigidbody r = transforms_user[i].GetComponent<Rigidbody> ();
				if (r != null) {
					r.isKinematic = kinematicSetting;
				}
			}
		}


    }
}
