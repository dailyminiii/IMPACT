using System;
using UnityEngine;
using NeuronDataReaderManaged;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MixedReality.Toolkit.UX; // MRTK UI 사용
using TMPro; // TextMeshPro 사용

namespace Neuron
{
	public class IMPACTFeedback : NeuronInstance
	{
        private scaleController scaleControllerInstance;

        [Space(10)]
        public bool enableHipMove = true;
        public bool enableFingerMove = true;
        public Transform					root_expert = null;
        public Transform					root_user = null;

        public string folderPath = "Assets/FeedbackSample/";
        public string subject = "Sub00";
        public int strokeNumber = 00;

        // 그래프 객체를 Unity Inspector에서 연결
        public GameObject flexionGraph;
        public GameObject extensionGraph;

        public Material highlightMaterial;

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

        [SerializeField] private GameObject ExpertArm;
        [SerializeField] private GameObject PlayerArm;
        [SerializeField] private UserDataRecorder UserDataRecorder;


        [Header("Guidance control variables")]
		[Range(0.1f, 1f)]
        public Vector3 positionOffset_Expert = new Vector3(0f, 0f, 0f);
        public Vector3 positionOffset_User = new Vector3(0f, 0f, 0f);

        public float armFlexionMuscleActivation_Expert = 0.5f;
        public float armExtensionMuscleActivation_Expert = 0.5f;
        public float forearmFlexionMuscleActivation_Expert = 0.5f;
        public float forearmExtensionMuscleActivation_Expert = 0.5f;

        public float armFlexionMuscleActivation_User = 0.5f;
        public float armExtensionMuscleActivation_User = 0.5f;
        public float forearmFlexionMuscleActivation_User = 0.5f;
        public float forearmExtensionMuscleActivation_User = 0.5f;

        public TextMeshProUGUI resultText;

        public int windowSize;

        [Range(0f, 1f)] public float armFlexionDifference;
        [Range(0f, 1f)] public float armExtensionDifference;
        [Range(0f, 1f)] public float forearmFlexionDifference;
        [Range(0f, 1f)] public float forearmExtensionDifference;

        public GameObject armExtensionMuscle_Expert;
        public GameObject armFlexionMuscle_Expert;
        public GameObject forearmExtensionMuscle_Expert;
        public GameObject forearmFlexionMuscle_Expert;
        public GameObject armExtensionMuscle_Expert_visualizatoin;
        public GameObject armFlexionMuscle_Expert_visualizatoin;
        public GameObject forearmExtensionMuscle_Expert_visualizatoin;
        public GameObject forearmFlexionMuscle_Expert_visualizatoin;
        public GameObject Hips_Expert;

        public GameObject armExtensionMuscle_User;
        public GameObject armFlexionMuscle_User;
        public GameObject forearmExtensionMuscle_User;
        public GameObject forearmFlexionMuscle_User;
        public GameObject armExtensionMuscle_User_visualizatoin;
        public GameObject armFlexionMuscle_User_visualizatoin;
        public GameObject forearmExtensionMuscle_User_visualizatoin;
        public GameObject forearmFlexionMuscle_User_visualizatoin;
        public GameObject Hips_User;
        public GameObject Player;

        private ButterworthFilter[] bandpassFilters;

        public float alpha;
        public float threshold;

        private speedController speedCtrl; // speedController 인스턴스 저장
        public float speedScaler; // 기본 속도 스케일러 값

        [SerializeField] private PressableButton startButton; // MRTK PressableButton
        [SerializeField] private TextMeshProUGUI countdownText; // 카운트다운 표시 UI
        [SerializeField] private AudioSource ttsAudioSource; // AudioSource (TTS 재생)
        [SerializeField] private AudioClip[] countdownClips; // "5, 4, 3, 2, 1, Go!" 오디오 클립 배열
        [SerializeField] private AudioSource audioSource_forearm;
        [SerializeField] private AudioSource audioSource_arm;

        [SerializeField] private ArmMuscleHighlighter realTimeUserArmScript;
        [SerializeField] private ForearmMuscleHighlighter realTimeUserForearmScript;



        private Dictionary<int, float[]> ArmMuscle_Expert = new Dictionary<int, float[]>();
        private Dictionary<int, float[]> ArmMuscle_User = new Dictionary<int, float[]>();

        // 클래스 멤버 변수로 선언
        private float RtArmFlexion, RtArmExtension;
        private float RtForearmFlexion, RtForeamExtension;

        private TargetUser previousTargetUser;


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
        public NeuronTransformsPhysicalReference	physicalReference_User = new NeuronTransformsPhysicalReference();
		Vector3[]							bonePositionOffsets_User = new Vector3[(int)NeuronBones_simple.NumOfBones];
		Vector3[]							boneRotationOffsets_User = new Vector3[(int)NeuronBones_simple.NumOfBones];

        Quaternion[] orignalRot_User = new Quaternion[(int)NeuronBones_simple.NumOfBones];
        Quaternion[] orignalParentRot_User = new Quaternion[(int)NeuronBones_simple.NumOfBones];
        Vector3[] orignalPositions_User = new Vector3[(int)NeuronBones_simple.NumOfBones];

        [HideInInspector]
        public bool[] disableBoneMovement_User = new bool[(int)NeuronBones_simple.NumOfBones];

        private Color defaultColor = Color.white; // 기본 색상
        private Color highlightColor = Color.red; // 강조 색상


		// Data extracted from CSV file were saved 
		private Dictionary<int, Quaternion[]> jointRotationData_User = new Dictionary<int, Quaternion[]>();
        private Dictionary<int, Vector3[]> jointPositionData_User = new Dictionary<int, Vector3[]>();
        public enum TargetUser
        {
            Expert,
            User,
            Replay,
            UserEnd
            
        }

        public enum TargetFeedback
        {
            VisualFeedback,
            AuditoryFeedback,
            
        }

        public TargetUser selectedTargetUser;

        public TargetFeedback selectedFeedback;

        public void SetTargetUser(TargetUser targetUser)
        {
            selectedTargetUser = targetUser;
            Debug.Log($"Target User Updated: {selectedTargetUser}");
        }

        public void SetTargetFeedback(TargetFeedback targetFeedback)
        {
            selectedFeedback = targetFeedback;
        }


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

            // RealTimeUser 오브젝트에서 IMPACTFeedback 스크립트 찾기
            realTimeUserArmScript = FindObjectOfType<ArmMuscleHighlighter>();

            if (realTimeUserArmScript == null)
            {
                Debug.LogError("realTimeUserArmScript 스크립트를 찾을 수 없습니다.");
            }

            // RealTimeUser 오브젝트에서 IMPACTFeedback 스크립트 찾기
            realTimeUserForearmScript = FindObjectOfType<ForearmMuscleHighlighter>();

            if (realTimeUserForearmScript == null)
            {
                Debug.LogError("realTimeUserForearmScript 스크립트를 찾을 수 없습니다.");
            }

            SetObjectActive(ExpertArm, false);
            SetObjectActive(Player, false);
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

            if (realTimeUserArmScript != null)
            {
                // 매 프레임마다 최신 값 가져오기
                (RtArmFlexion, RtArmExtension) = realTimeUserArmScript.GetActivationValues();
            }

            if (realTimeUserForearmScript != null)
            {
                // 매 프레임마다 최신 값 가져오기
                (RtForearmFlexion, RtForeamExtension) = realTimeUserForearmScript.GetActivationValues();
            }

            // 선택된 Target Muscle에 따라 차이 계산 및 업데이트
            switch (selectedTargetUser)
            {
                case TargetUser.Expert:
                    SetObjectActive(Player, false);
                    forearmFlexionDifference = forearmFlexionMuscleActivation_Expert;
                    forearmExtensionDifference = forearmExtensionMuscleActivation_Expert;
                    armFlexionDifference = armFlexionMuscleActivation_Expert;
                    armExtensionDifference = armExtensionMuscleActivation_Expert;

                    switch (selectedFeedback)
                    {
                        case TargetFeedback.VisualFeedback:
                            Debug.Log("Play Visual");
                            break;
                        case TargetFeedback.AuditoryFeedback:
                            Debug.Log("Play Sound");
                            break;
                    }
                    break;

                case TargetUser.User:
                    SetObjectActive(Player, false);
                    forearmFlexionDifference = Mathf.Abs(RtForearmFlexion);
                    forearmExtensionDifference = Mathf.Abs(RtForeamExtension);
                    armFlexionDifference = Mathf.Abs(RtArmFlexion);
                    armExtensionDifference = Mathf.Abs(RtArmExtension);
                    
                    switch (selectedFeedback)
                    {
                        case TargetFeedback.VisualFeedback:
                            PlayVisual();
                            Debug.Log("Play Visual");
                            break;
                        case TargetFeedback.AuditoryFeedback:
                            PlaySound(audioSource_forearm, audioSource_arm);
                            Debug.Log("Play Sound");
                            break;
                    }
                    break;
                case TargetUser.UserEnd:
                    SetObjectActive(Player, false);
                    break;
                case TargetUser.Replay:
                    SetObjectActive(Player, true);
                    break;
            }

            // 현재 선택값을 저장하여 다음 프레임에서 비교할 수 있도록 함
            previousTargetUser = selectedTargetUser;
            // 선택된 Target Feedback이 Visual일 경우 ExpertArm과 PlayerArm을 활성화, 아니면 비활성화
            bool isVisualFeedback = selectedFeedback == TargetFeedback.VisualFeedback;
            SetObjectActive(ExpertArm, isVisualFeedback);
            

            // 근육 하이라이트 및 시각화
            if (armExtensionMuscle_Expert != null && armFlexionMuscle_Expert != null && forearmExtensionMuscle_Expert != null && forearmFlexionMuscle_Expert != null)
            {
                HighlightRightArm(armExtensionMuscle_Expert, armFlexionMuscle_Expert, forearmExtensionMuscle_Expert,  forearmFlexionMuscle_Expert);
                VisualizeMuscleActivation(armFlexionMuscle_Expert, armExtensionMuscle_Expert, forearmFlexionMuscle_Expert, forearmExtensionMuscle_Expert, armExtensionMuscleActivation_Expert, armExtensionMuscleActivation_Expert, forearmFlexionMuscleActivation_Expert, forearmExtensionMuscleActivation_Expert);
                
            }

    
            if (armExtensionMuscle_User != null && armFlexionMuscle_User != null && forearmExtensionMuscle_User != null && forearmFlexionMuscle_User != null)
            {
                HighlightRightArm(armExtensionMuscle_User, armFlexionMuscle_User, forearmExtensionMuscle_User,  forearmFlexionMuscle_User);
                VisualizeMuscleActivation(armFlexionMuscle_User, armExtensionMuscle_User, forearmFlexionMuscle_User, forearmExtensionMuscle_User, armExtensionMuscleActivation_User, armExtensionMuscleActivation_User, forearmFlexionMuscleActivation_User, forearmExtensionMuscleActivation_User);
                
            }

            if (speedCtrl != null)
            {
                //speedScaler = speedCtrl.SpeedScaler; // speedScaler 값 업데이트
            }

            
            
        }

        //시작 프레임 값을 업데이트하는 메서드
        public void SetFeedbackStartFrame(int frame) {
            feedbackStartFrame = Mathf.Clamp(frame, 0, 89);
            Debug.Log($"Feedback Start Frame Updated: {feedbackStartFrame}");
        }

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

        public void ActiveFeedback()
        {
            switch (selectedFeedback)
                            {
                case TargetFeedback.VisualFeedback:
                    SetObjectActive(Player, false);
                    ActivateVisual();
                    break;

                case TargetFeedback.AuditoryFeedback:
                    SetObjectActive(Player, false);
                    ActivateSound();
                    break;

}
        }

        public void ActivateReplay()
        {
            // Base path where CSV files are stored
            string basePath = $"{subject}/";
            string subCode;

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
            string ExpertMVCArm = $"{ExpertMVCbasePath}{subCode}Forearm.csv";

            ReadCSV_Expert(ExpertCSV, ExpertMVCForearm, ExpertMVCArm, jointRotationData_Expert, jointPositionData_Expert);
            ReadCSV_User(UserCSV, UserMVCForearm, UserMVCArm, jointRotationData_User, jointPositionData_User);
            Normalization();


            StartMotion(ExpertCSV, UserCSV, "replay");

        }

        // public void TargetMuscleRecommend()
        // {
        //     // Base path where CSV files are stored
        //     string basePath = $"{subject}/";
        //     string subCode;

        //     string ExpertMVCbasePath = $"ExpertMVC/";

        //     // Find the matching expert file
        //     string ExpertCSV = FindMatchingExpertCSV(basePath, subject, strokeNumber, out subCode);
        //     if (string.IsNullOrEmpty(ExpertCSV))
        //     {
        //         Debug.LogError("No matching expert file found. Cannot proceed.");
        //         return;
        //     }


        //     // User file path
        //     string UserCSV = $"{basePath}{subject}_Session{strokeNumber}.csv";
        //     string UserMVCForearm = $"{basePath}{subject}Forearm.csv";
        //     string UserMVCArm = $"{basePath}{subject}Arm.csv";

        //     string ExpertMVCForearm = $"{ExpertMVCbasePath}{subCode}Forearm.csv";
        //     string ExpertMVCArm = $"{ExpertMVCbasePath}{subCode}Forearm.csv";

        //     ReadCSV_Expert(ExpertCSV, ExpertMVCForearm, ExpertMVCArm, jointRotationData_Expert, jointPositionData_Expert);
        //     ReadCSV_User(UserCSV, UserMVCForearm, UserMVCArm, jointRotationData_User, jointPositionData_User);
        //     Normalization();

        //     // 근육 그룹 정의 (이름 포함)
        //     Dictionary<string, int[]> muscleGroups = new Dictionary<string, int[]>
        //     {
        //         { "forearm flexion muscle group", new int[] { 0, 1, 7 } },  
        //         { "forearm extension muscle group", new int[] { 3, 4, 5 } },
        //         { "arm flexion muscle group", new int[] { 8, 9, 15 } },
        //         { "arm extension muscle group", new int[] { 11, 12, 13 } }
        //     };

        //     Dictionary<string, float> dtwResults = new Dictionary<string, float>();

        //     // 각 근육 그룹에 대해 DTW 계산 및 UI 업데이트
        //     //string resultMessage = "<size=80%><b>DTW Similarity Scores</b></size>\n\n";
        //     string resultMessage = "";

        //     foreach (var muscleGroup in muscleGroups)
        //     {
        //         float dtwValue = ComputeDTW(ArmMuscle_Expert, ArmMuscle_User, muscleGroup.Value);
        //         dtwResults[muscleGroup.Key] = dtwValue;

        //         // 텍스트 줄 간격 및 스타일 적용
        //         //resultMessage += $"<b><line-height=200%>{muscleGroup.Key}:</b> {dtwValue:F2}</line-height>\n";
        //     }

        //     // 가장 DTW 값이 큰 근육 그룹을 타겟으로 선택
        //     var targetMuscle = dtwResults.OrderByDescending(x => x.Value).First();

        //     //resultMessage += $"\n<size=80%><color=yellow><line-height=200%>Recommended Target Muscle:</line-height></color></size>\n";
        //     resultMessage += $"<size=80%><color=yellow><b>{targetMuscle.Key}</b></color></size>";

        //     // 최종 결과를 UI에 업데이트
        //     UpdateUIText(resultMessage);

        //     Debug.Log($"Recommended Target Muscle: {targetMuscle.Key} with DTW value: {targetMuscle.Value:F2}");

        //     // Highlight the corresponding muscle group
        //     HighlightMuscleGroup(targetMuscle.Key);
        // }

        public void Replayclicked()
        {
            //SetObjectActive(Player, true);
            ActivateReplay();
        }


        // Highlight the recommended muscle group
        public void HighlightMuscleGroup(string muscleGroup)
        {
            // 근육 그룹에 대응하는 GameObject 매핑
            Dictionary<string, GameObject> muscleObjects = new Dictionary<string, GameObject>
            {
                { "forearm flexion muscle group", forearmFlexionMuscle_User_visualizatoin },
                { "forearm extension muscle group", forearmExtensionMuscle_User_visualizatoin },
                { "arm flexion muscle group", armFlexionMuscle_User_visualizatoin },
                { "arm extension muscle group", armExtensionMuscle_User_visualizatoin }
            };

            // 모든 근육 GameObject의 색상을 기본 색상으로 변경
            foreach (var muscleObject in muscleObjects.Values)
            {
                if (muscleObject != null)
                {
                    Renderer renderer = muscleObject.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = defaultColor; // 기본 색상 설정
                    }
                }
            }

            // 선택된 근육 그룹의 색상을 강조 색상으로 변경
            if (muscleObjects.ContainsKey(muscleGroup) && muscleObjects[muscleGroup] != null)
            {
                Renderer renderer = muscleObjects[muscleGroup].GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = highlightColor; // 강조 색상 설정
                }
            }
        }

        private void ActivateVisual()
        {
            // Base path where CSV files are stored
            string basePath = $"{subject}/";
            string subCode;

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
            string ExpertMVCArm = $"{ExpertMVCbasePath}{subCode}Forearm.csv";

            ReadCSV_Expert(ExpertCSV, ExpertMVCForearm, ExpertMVCArm, jointRotationData_Expert, jointPositionData_Expert);
            ReadCSV_User(UserCSV, UserMVCForearm, UserMVCArm, jointRotationData_User, jointPositionData_User);
            Normalization();


            StartMotion(ExpertCSV, UserCSV, "visual");
        }

        private void ActivateSound()
        {
            // Base path where CSV files are stored
            string basePath = $"{subject}/";
            string subCode;

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
            string ExpertMVCArm = $"{ExpertMVCbasePath}{subCode}Forearm.csv";

            ReadCSV_Expert(ExpertCSV, ExpertMVCForearm, ExpertMVCArm, jointRotationData_Expert, jointPositionData_Expert);
            ReadCSV_User(UserCSV, UserMVCForearm, UserMVCArm, jointRotationData_User, jointPositionData_User);
            Normalization();


            StartMotion(ExpertCSV, UserCSV, "sound");
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
            string[] allFiles = Directory.GetFiles(fullPath);

            // Define the pattern with a capture group for SubXX
            string pattern = $"{subject}_Session{strokeNumber}_expert_(Sub\\d{{2}})\\.csv";
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

            Debug.LogError($"No matching expert file found for {subject} Session {strokeNumber}");
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

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;

                string[] parts = lines[i].Split(',');
                Quaternion[] rotations = new Quaternion[21];
				Vector3[] positions = new Vector3[21];
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


					positions[j] = givenLocalPosition;
                    rotations[j] = givenQuaternion;
                }

                // Parse muscle data
                try
                {
                    for (int j = 0; j < 16; j += 1)
                    {
                        // Parse raw muscle data
                        muscle[j] = float.TryParse(parts[j], out float parsedX) ? parsedX : 0f;

                        // Butterworth Filtering
                        filteredMuscle[j] = bandpassFilters[j].Apply(muscle[j]);

                        // Rectification
                        rectifiedMuscle[j] = Mathf.Abs(filteredMuscle[j]);
                        rectifiedBuffers[j].Add(rectifiedMuscle[j]);
                        if (rectifiedBuffers[j].Count > windowSize)
                        {
                            rectifiedBuffers[j].RemoveAt(0);
                        }

                        // RMS Calculation
                        if (rectifiedBuffers[j].Count == windowSize)
                        {
                            rmsMuscle[j] = Mathf.Sqrt(rectifiedBuffers[j].Sum(x => x * x) / windowSize);
                        }
                        else
                        {
                            rmsMuscle[j] = 0f; // Insufficient data
                        }

                        // MVC Normalization
                        rmsMuscle[j] = Mathf.Clamp(rmsMuscle[j] / mvcValues[j], 0f, 1f); // Normalize using MVC
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing EMG data at frame {i}. Exception: {ex.Message}");
                    muscle = new float[16]; // Default to zero values if parsing fails
                }

                jointPositionData_Expert[i] = positions;
                jointRotationData_Expert[i] = rotations;
                ArmMuscle_Expert[i] = rmsMuscle;
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


            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;

                string[] parts = lines[i].Split(',');
                Quaternion[] rotations = new Quaternion[21];
				Vector3[] positions = new Vector3[21];
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

					positions[j] = givenLocalPosition;
                    rotations[j] = givenQuaternion;
                }

                // Parse muscle data
                try
                {
                    for (int j = 0; j < 16; j += 1)
                    {
                        // Parse raw muscle data
                        muscle[j] = float.TryParse(parts[j], out float parsedX) ? parsedX : 0f;

                        // Butterworth Filtering
                        filteredMuscle[j] = bandpassFilters[j].Apply(muscle[j]);

                        // Rectification
                        rectifiedMuscle[j] = Mathf.Abs(filteredMuscle[j]);
                        rectifiedBuffers[j].Add(rectifiedMuscle[j]);
                        if (rectifiedBuffers[j].Count > windowSize)
                        {
                            rectifiedBuffers[j].RemoveAt(0);
                        }

                        // RMS Calculation
                        if (rectifiedBuffers[j].Count == windowSize)
                        {
                            rmsMuscle[j] = Mathf.Sqrt(rectifiedBuffers[j].Sum(x => x * x) / windowSize);
                        }
                        else
                        {
                            rmsMuscle[j] = 0f; // Insufficient data
                        }

                        // MVC Normalization
                        rmsMuscle[j] = Mathf.Clamp(rmsMuscle[j] / mvcValues[j], 0f, 1f); // Normalize using MVC
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing EMG data at frame {i}. Exception: {ex.Message}");
                    muscle = new float[16]; // Default to zero values if parsing fails
                }

                jointPositionData_User[i] = positions;
                jointRotationData_User[i] = rotations;
                ArmMuscle_User[i] = rmsMuscle;
            }

            return true;
        }

        private float[] LoadMVCValuesFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"File not found: {filePath}");
                return null;
            }

            string[] lines = File.ReadAllLines(filePath);
            float[] mvcValues = new float[8]; // Assume 8 channels per file
            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                string[] parts = lines[i].Split(',');
                int channel = int.Parse(parts[0].Replace("Channel ", "").Trim()) - 1;
                float value = float.Parse(parts[1].Trim());
                mvcValues[channel] = value;
            }

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
                // Precomputed coefficients for 10–40Hz, 2nd-order Butterworth filter, 50Hz sampling rate
                b = new float[] { 0.2066f, 0f, -0.4131f, 0f, 0.2066f };
                a = new float[] { 1f, -1.3730f, 1.1040f, -0.3433f, 0.0717f };

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

            if (feedbacktype == "sound")
            {
                // 코루틴 실행: Motion 적용 및 진동 데이터 전송
                StartCoroutine(StartMotionAndSendSound(participant1, participant2));
            }
            else if (feedbacktype == "visual")
            {
                // 코루틴 실행: Motion 적용 및 진동 데이터 전송
                StartCoroutine(StartMotionAndSendVisual(participant1, participant2));
            }

            else if (feedbacktype == "replay")
            {
                // 코루틴 실행: Motion 적용 및 진동 데이터 전송
                StartCoroutine(StartOnlyMotion(participant1, participant2));
            }
        }

        private IEnumerator StartMotionAndSendSound(string participant1, string participant2)
        {
            int expertFrameCount = feedbackStartFrame;
            int userFrameCount = feedbackStartFrame;
            int totalFrames = Mathf.Max(jointRotationData_Expert.Count, jointRotationData_User.Count); // 전체 프레임 수 계산

            bool isFirstExpertFrame = true;


            Vector3 firstHipPositionExpert = Vector3.zero;

            float forearmFlexionEMAExpert = 0f;
            float forearmExtensionEMAExpert = 0f;
            float armFlexionEMAExpert = 0f;
            float armExtensionEMAExpert = 0f;

            
            if (audioSource_forearm == null)
            {
                audioSource_forearm = gameObject.AddComponent<AudioSource>();
            }

            if (audioSource_arm == null)
            {
                audioSource_arm = gameObject.AddComponent<AudioSource>();
            }

            while (expertFrameCount < jointRotationData_Expert.Count || userFrameCount < jointRotationData_User.Count)
            {
                // Expert 데이터 처리
                if (expertFrameCount < jointRotationData_Expert.Count)
                {
                    ApplyFrameToTransform(
                        transforms_expert,
                        jointRotationData_Expert[expertFrameCount],
                        jointPositionData_Expert[expertFrameCount],
                        ArmMuscle_Expert[expertFrameCount],
                        bonePositionOffsets_Expert,
                        boneRotationOffsets_Expert,
                        orignalRot_Expert,
                        orignalParentRot_Expert,
                        ref firstHipPositionExpert,
                        isFirstExpertFrame,
                        enableHipMove,
                        disableBoneMovement_Expert,
                        ref forearmFlexionEMAExpert,
                        ref forearmExtensionEMAExpert,
                        ref armFlexionEMAExpert,
                        ref armExtensionEMAExpert,
                        ref forearmFlexionMuscleActivation_Expert,
                        ref forearmExtensionMuscleActivation_Expert,
                        ref armFlexionMuscleActivation_Expert,
                        ref armExtensionMuscleActivation_Expert,
                        alpha,
                        threshold,
                        orignalPositions_Expert // 추가
                    );
                    isFirstExpertFrame = false; // 첫 프레임 처리 완료
                }

                // 사운드 재생
                PlaySound(audioSource_forearm, audioSource_arm);

                // 프레임 증가
                if (expertFrameCount < jointRotationData_Expert.Count) expertFrameCount++;
                if (userFrameCount < jointRotationData_User.Count) userFrameCount++;

                yield return new WaitForSeconds(1f / (30f * speedScaler)); // 30 FPS 기준 대기
            }

            // 모든 프레임이 끝나면 사운드 정지
            StopSound(audioSource_forearm, audioSource_arm);
        }

        private void PlaySound(AudioSource forearmAudioSource, AudioSource armAudioSource)
        {
            float forearmMuscleDifference = forearmFlexionDifference;
            float armMuscleDifference = armFlexionDifference;

            // Threshold 이상의 값만 반응
            float forearmVolume = Mathf.Clamp(forearmMuscleDifference, 0f, 1f);
            float armVolume = Mathf.Clamp(armMuscleDifference, 0f, 1f);

            // 주파수 설정
            float forearmFixedFrequency = 1318.5f;
            float armFixedFrequency = 1568f;

            // Forearm용 AudioClip 생성 및 재생
            forearmAudioSource.clip = GenerateSineWave(forearmFixedFrequency);
            forearmAudioSource.volume = forearmVolume;
            
            if (!forearmAudioSource.isPlaying)
            {
                forearmAudioSource.Play();
            }

            // Arm용 AudioClip 생성 및 재생
            armAudioSource.clip = GenerateSineWave(armFixedFrequency);
            armAudioSource.volume = armVolume;
            
            if (!armAudioSource.isPlaying)
            {
                armAudioSource.Play();
            }
        }


        private void StopSound(AudioSource forearmAudioSource, AudioSource armAudioSource)
        {
            if (forearmAudioSource.isPlaying)
            {
                forearmAudioSource.Stop();
            }
            
            if (armAudioSource.isPlaying)
            {
                armAudioSource.Stop();
            }
        }

        private AudioClip GenerateSineWave(float frequency)
        {
            int sampleRate = 44100;
            float duration = 0.1f; // 100ms duration
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate);
            }

            AudioClip clip = AudioClip.Create("SineWave", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private IEnumerator StartOnlyMotion(string participant1, string participant2)
        {
            int expertFrameCount = feedbackStartFrame;
            int userFrameCount = feedbackStartFrame;
            int totalFrames = Mathf.Max(jointRotationData_Expert.Count, jointRotationData_User.Count); // 전체 프레임 수 계산

            bool isFirstExpertFrame = true;
            bool isFirstUserFrame = true;

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

            List<float> flexionDifferences = new List<float>();
            List<float> extensionDifferences = new List<float>();

            while (expertFrameCount < jointRotationData_Expert.Count || userFrameCount < jointRotationData_User.Count)
            {
                // Expert 데이터 처리
                if (expertFrameCount < jointRotationData_Expert.Count)
                {
                    ApplyFrameToTransform(
                        transforms_expert,
                        jointRotationData_Expert[expertFrameCount],
                        jointPositionData_Expert[expertFrameCount],
                        ArmMuscle_Expert[expertFrameCount],
                        bonePositionOffsets_Expert,
                        boneRotationOffsets_Expert,
                        orignalRot_Expert,
                        orignalParentRot_Expert,
                        ref firstHipPositionExpert,
                        isFirstExpertFrame,
                        enableHipMove,
                        disableBoneMovement_Expert,
                        ref forearmFlexionEMAExpert,
                        ref forearmExtensionEMAExpert,
                        ref armFlexionEMAExpert,
                        ref armExtensionEMAExpert,
                        ref forearmFlexionMuscleActivation_Expert,
                        ref forearmExtensionMuscleActivation_Expert,
                        ref armFlexionMuscleActivation_Expert,
                        ref armExtensionMuscleActivation_Expert,
                        alpha,
                        threshold,
                        orignalPositions_Expert // 추가
                    );
                    isFirstExpertFrame = false; // 첫 프레임 처리 완료
                }

                // User 데이터 처리
                if (userFrameCount < jointRotationData_User.Count)
                {
                    ApplyFrameToTransform(
                        transforms_user,
                        jointRotationData_User[userFrameCount],
                        jointPositionData_User[userFrameCount],
                        ArmMuscle_User[userFrameCount],
                        bonePositionOffsets_User,
                        boneRotationOffsets_User,
                        orignalRot_User,
                        orignalParentRot_User,
                        ref firstHipPositionUser,
                        isFirstUserFrame,
                        enableHipMove,
                        disableBoneMovement_User,
                        ref forearmFlexionEMAUser,
                        ref forearmExtensionEMAUser,
                        ref armFlexionEMAUser,
                        ref armExtensionEMAUser,
                        ref forearmFlexionMuscleActivation_User,
                        ref forearmExtensionMuscleActivation_User,
                        ref armFlexionMuscleActivation_User,
                        ref armExtensionMuscleActivation_User,
                        alpha,
                        threshold,
                        orignalPositions_User // 추가
                    );
                    isFirstUserFrame = false; // 첫 프레임 처리 완료
                }

                // 차이 계산 및 그래프 데이터 저장
                float flexionDifference = Mathf.Abs(forearmFlexionMuscleActivation_Expert);
                float extensionDifference = Mathf.Abs(forearmFlexionMuscleActivation_User);

                flexionDifferences.Add(flexionDifference);
                extensionDifferences.Add(extensionDifference);


                // 프레임 증가
                if (expertFrameCount < jointRotationData_Expert.Count) expertFrameCount++;
                if (userFrameCount < jointRotationData_User.Count) userFrameCount++;

                yield return new WaitForSeconds(1f / (30f * speedScaler)); // 30 FPS 기준 대기
            }
        }


        private IEnumerator StartMotionAndSendVisual(string participant1, string participant2)
        {
            int expertFrameCount = feedbackStartFrame;
            int userFrameCount = feedbackStartFrame;
            int totalFrames = Mathf.Max(jointRotationData_Expert.Count, jointRotationData_User.Count); // 전체 프레임 수 계산

            bool isFirstExpertFrame = true;

            Vector3 firstHipPositionExpert = Vector3.zero;

            float forearmFlexionEMAExpert = 0f;
            float forearmExtensionEMAExpert = 0f;
            float armFlexionEMAExpert = 0f;
            float armExtensionEMAExpert = 0f;

            while (expertFrameCount < jointRotationData_Expert.Count || userFrameCount < jointRotationData_User.Count)
            {
                // Expert 데이터 처리
                if (expertFrameCount < jointRotationData_Expert.Count)
                {
                    ApplyFrameToTransform(
                        transforms_expert,
                        jointRotationData_Expert[expertFrameCount],
                        jointPositionData_Expert[expertFrameCount],
                        ArmMuscle_Expert[expertFrameCount],
                        bonePositionOffsets_Expert,
                        boneRotationOffsets_Expert,
                        orignalRot_Expert,
                        orignalParentRot_Expert,
                        ref firstHipPositionExpert,
                        isFirstExpertFrame,
                        enableHipMove,
                        disableBoneMovement_Expert,
                        ref forearmFlexionEMAExpert,
                        ref forearmExtensionEMAExpert,
                        ref armFlexionEMAExpert,
                        ref armExtensionEMAExpert,
                        ref forearmFlexionMuscleActivation_Expert,
                        ref forearmExtensionMuscleActivation_Expert,
                        ref armFlexionMuscleActivation_Expert,
                        ref armExtensionMuscleActivation_Expert,
                        alpha,
                        threshold,
                        orignalPositions_Expert // 추가
                    );
                    isFirstExpertFrame = false; // 첫 프레임 처리 완료
                }

                // 시각적 피드백 적용
                PlayVisual();

                // 프레임 증가
                if (expertFrameCount < jointRotationData_Expert.Count) expertFrameCount++;
                if (userFrameCount < jointRotationData_User.Count) userFrameCount++;

                yield return new WaitForSeconds(1f / (30f * speedScaler)); // 30 FPS 기준 대기
            }
        }

        // 시각적 피드백 적용 메서드
        private void PlayVisual()
        {
            // Threshold 이상의 값만 반응
            float intensity_forearm = forearmFlexionDifference;
            float intensity_arm = armFlexionDifference;

            // 색상 변화 적용 (활성도 차이에 따라 색상이 더 강해짐)
            Color baseColor = new Color(1f, 0.8f, 0.8f, 1f); // 기본 빨간색 (약한 활성화)
            Color highlightedColor = new Color(1f, 0f, 0f, 1f); // 강한 활성화 (진한 빨간색)


            Color finalColor_forearm = Color.Lerp(baseColor, highlightedColor, intensity_forearm);
            Color finalColor_arm = Color.Lerp(baseColor, highlightedColor, intensity_arm);

            ApplyVisualFeedback(forearmFlexionMuscle_Expert_visualizatoin, finalColor_forearm);
            ApplyVisualFeedback(armFlexionMuscle_Expert_visualizatoin, finalColor_arm);
            ApplyVisualFeedback(forearmExtensionMuscle_Expert_visualizatoin, finalColor_forearm);
            ApplyVisualFeedback(armExtensionMuscle_Expert_visualizatoin, finalColor_arm);
        }

        // 특정 근육 오브젝트에 색상을 적용하는 함수
        private void ApplyVisualFeedback(GameObject muscleObject, Color color)
        {
            if (muscleObject == null) return;

            Renderer renderer = muscleObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }


        private void ApplyFrameToTransform(
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
            ref float forearmFlexionActivation,
            ref float forearmExtensionActivation,
            ref float armFlexionActivation,
            ref float armExtensionActivation,
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
            // Compute RMS Normalization and EMA
            float normForearmFlexion = RMSNormalize(new float[] { ArmMuscle[0], ArmMuscle[1], ArmMuscle[2], ArmMuscle[3], ArmMuscle[4], ArmMuscle[5], ArmMuscle[6], ArmMuscle[7] }, new float[] { 1, 1, 1, 1, 1, 1, 1, 1 });
            float normForearmExtension = RMSNormalize(new float[] { ArmMuscle[0], ArmMuscle[1], ArmMuscle[2], ArmMuscle[3], ArmMuscle[4], ArmMuscle[5], ArmMuscle[6], ArmMuscle[7] }, new float[] { 1, 1, 1, 1, 1, 1, 1, 1 });
            float normArmFlexion = RMSNormalize(new float[] { ArmMuscle[8], ArmMuscle[9], ArmMuscle[10], ArmMuscle[11], ArmMuscle[12], ArmMuscle[13], ArmMuscle[14], ArmMuscle[15] }, new float[] { 1, 1, 1, 1, 1, 1, 1, 1 });
            float normArmExtension = RMSNormalize(new float[] { ArmMuscle[8], ArmMuscle[9], ArmMuscle[10], ArmMuscle[11], ArmMuscle[12], ArmMuscle[13], ArmMuscle[14], ArmMuscle[15] }, new float[] { 1, 1, 1, 1, 1, 1, 1, 1 });

            forearmFlexionEMA = ApplyExponentialMovingAverage(normForearmFlexion, forearmFlexionEMA, alpha);
            forearmExtensionEMA = ApplyExponentialMovingAverage(normForearmExtension, forearmExtensionEMA, alpha);
            armFlexionEMA = ApplyExponentialMovingAverage(normArmFlexion, armFlexionEMA, alpha);
            armExtensionEMA = ApplyExponentialMovingAverage(normArmExtension, armExtensionEMA, alpha);

            // Apply Threshold Damping
            forearmFlexionActivation = ApplyThresholdDamping(forearmFlexionEMA, forearmFlexionActivation, threshold);
            forearmExtensionActivation = ApplyThresholdDamping(forearmExtensionEMA, forearmExtensionActivation, threshold);
            armFlexionActivation = ApplyThresholdDamping(armFlexionEMA, armFlexionActivation, threshold);
            armExtensionActivation = ApplyThresholdDamping(armExtensionEMA, armExtensionActivation, threshold);

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

        private float RMSNormalize(float[] emgValues, float[] mvcValues)
        {
            float sumOfSquares = 0f;
            for (int i = 0; i < emgValues.Length; i++)
            {
                float normalizedValue = NormalizeEMGValue(emgValues[i], mvcValues[i]);
                sumOfSquares += normalizedValue * normalizedValue;
            }
            return Mathf.Sqrt(sumOfSquares / emgValues.Length);
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
            this.root_user = root_user;
            this.prefix_user = prefix_user;
			//int bound_count = 
            NeuronHelper.Bind( root_user, transforms_user, prefix_user, false, (skeletonType == NeuronEnums.SkeletonType.PerceptionNeuronStudio) ? NeuronBoneVersion.V1 : NeuronBoneVersion.V2);
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

        // 오른쪽 팔 하이라이트
        void HighlightRightArm(GameObject armFlexionMuscle, GameObject armExtensionMuscle, GameObject forearmFlexionMuscle, GameObject forearmExtensionMuscle)
        {
            ApplyMaterialToTransform(armFlexionMuscle.transform, highlightMaterial);
            ApplyMaterialToTransform(armExtensionMuscle.transform, highlightMaterial);
            ApplyMaterialToTransform(forearmFlexionMuscle.transform, highlightMaterial);
            ApplyMaterialToTransform(forearmExtensionMuscle.transform, highlightMaterial);
        }

        // 근육 활성도 시각화
        void VisualizeMuscleActivation(GameObject armFlexionMuscle, GameObject armExtensionMuscle, GameObject forearmFlexionMuscle, GameObject forearmExtensionMuscle, float armFlexionMuscleActivation, float armExtensionMuscleActivation, float forearmFlexionMuscleActivation, float forearmExtensionMuscleActivation)
        {
            VisualizeMuscleActivationOnTransform(armFlexionMuscle.transform, armFlexionMuscleActivation);
            VisualizeMuscleActivationOnTransform(armExtensionMuscle.transform, armExtensionMuscleActivation);
            VisualizeMuscleActivationOnTransform(forearmFlexionMuscle.transform, forearmFlexionMuscleActivation);
            VisualizeMuscleActivationOnTransform(forearmExtensionMuscle.transform, forearmExtensionMuscleActivation);
        }

        // 특정 Transform에 Material 적용
        void ApplyMaterialToTransform(Transform targetTransform, Material material)
        {
            Renderer renderer = targetTransform.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = material;
            }
        }

        // 특정 Transform에 근육 활성도를 기반으로 색상 설정
        void VisualizeMuscleActivationOnTransform(Transform targetTransform, float activationLevel)
        {
            Renderer renderer = targetTransform.GetComponent<Renderer>();
            if (renderer != null)
            {
                // 활성도에 따라 색상 변경 (진한 빨간색 -> 연한 빨간색)
                Color deepRed = new Color(1f, 0f, 0f, 1f);  // 진한 빨간색 (알파값 1)
                Color lightRed = new Color(1f, 0.8f, 0.8f, 1f); // 연한 빨간색 (밝은 톤)

                // 활성도에 따라 두 색상을 혼합
                Color blendedColor = Color.Lerp(lightRed, deepRed, activationLevel);

                // 재질의 색상을 변경
                renderer.material.color = blendedColor;
            }
        }

        private float NormalizeEMGValue(float rawValue, float mvcValue)
        {
            return Mathf.Clamp01(rawValue / mvcValue);
        }

        private float ApplyExponentialMovingAverage(float newValue, float prevValue, float alpha = 0.2f)
        {
            return alpha * newValue + (1 - alpha) * prevValue;
        }

        private float ApplyThresholdDamping(float newValue, float prevValue, float threshold = 0.05f)
        {
            if (Mathf.Abs(newValue - prevValue) < threshold)
                return prevValue;
            return newValue;
        }

    }
}
