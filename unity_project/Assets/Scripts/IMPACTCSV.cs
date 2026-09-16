using System;
using UnityEngine;
using NeuronDataReaderManaged;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.IO;

namespace Neuron
{
	public class IMPACTCSV : NeuronInstance
	{
        [Space(10)]
        public bool enableHipMove = true;
        public bool enableFingerMove = true;
        public Transform					root = null;

        [HideInInspector]
        public string folderPath = "Assets/ExpertDatabase/";
        [HideInInspector]
        public string subject = "Sub11";
        [HideInInspector]
        public string strokeType = "Forehand Clear";
        [HideInInspector]
        public int strokeNumber = 1;

        private bool isPaused = false;
        public Material highlightMaterial;
        private Vector3 firstHipPosition = Vector3.zero; // 첫 프레임의 Hip 위치 저장
        //
        // Obsolete don't use it
        [HideInInspector]
		public string						prefix = "Robot_";
		public bool							boundTransforms { get ; private set; }
		public UpdateMethod					motionUpdateMethod = UpdateMethod.Normal;

        [HideInInspector]
        public Transform[]					transforms = new Transform[(int)NeuronBones_simple.NumOfBones];

        [Header("use an already existing NeuronTransformsInstance as the physical reference source")]
        [HideInInspector]
        public Transform					physicalReferenceOverride; //use an already existing NeuronAnimatorInstance as the physical reference


        [Header("Guidance control variables")]
		[HideInInspector]
        public TextAsset csvFile;
		[Range(0.1f, 1f)]
		public float speed = 1f;
        public Vector3 positionOffset = new Vector3(0f, 0f, 0f);

        private List<Quaternion[]> jointMuscle = new List<Quaternion[]>();

        [Range(0f, 1f)] public float armFlexionMuscleActivation = 0.5f;
        [Range(0f, 1f)] public float armExtensionMuscleActivation = 0.5f;
        [Range(0f, 1f)] public float forearmFlexionMuscleActivation = 0.5f;
        [Range(0f, 1f)] public float forearmExtensionMuscleActivation = 0.5f;

        private List<Vector3[]> expertPoseData = new List<Vector3[]>(); // 전문가 데이터 저장

        public GameObject armExtensionMuscle;
        public GameObject armFlexionMuscle;
        public GameObject forearmExtensionMuscle;
        public GameObject forearmFlexionMuscle;
        public GameObject Hips;


        public float mvcValue;
        public float alpha;
        public float threshold;

        private Dictionary<int, float[]> ArmMuscle = new Dictionary<int, float[]>();

        private float forearmFlexionPrevEMA = 0f;
        private float forearmExtensionPrevEMA = 0f;
        private float armFlexionPrevEMA = 0f;
        private float armExtensionPrevEMA = 0f;


        public NeuronTransformsPhysicalReference	physicalReference = new NeuronTransformsPhysicalReference();
		Vector3[]							bonePositionOffsets = new Vector3[(int)NeuronBones_simple.NumOfBones];
		Vector3[]							boneRotationOffsets = new Vector3[(int)NeuronBones_simple.NumOfBones];

        Quaternion[] orignalRot = new Quaternion[(int)NeuronBones_simple.NumOfBones];
        Quaternion[] orignalParentRot = new Quaternion[(int)NeuronBones_simple.NumOfBones];
        Vector3[] orignalPositions = new Vector3[(int)NeuronBones_simple.NumOfBones];

        [HideInInspector]
        public bool[] disableBoneMovement = new bool[(int)NeuronBones_simple.NumOfBones];


		// Data extracted from CSV file were saved 
		private Dictionary<int, Quaternion[]> jointRotationData = new Dictionary<int, Quaternion[]>();
        private Dictionary<int, Vector3[]> jointPositionData = new Dictionary<int, Vector3[]>();
        private Vector3 rootJointPosition = new Vector3(0f, 0f, 0f);




        bool inited = false;
		new void OnEnable()
		{
            if(inited)
            {
                return;
            }
            inited = true;

            //base.OnEnable();
			
			if( root == null )
			{
				root = transform;
			}

			Bind(root, prefix);
			//ReadCSV();
            //Normalization();
        }

        void Update()
        {

            // 30 FPS 기준으로 프레임 업데이트 주기 계산
            float frameDuration = 1f / 30f; // 30 Frame 기준 = 0.033초

            // 근육 하이라이트 및 시각화
            if (armExtensionMuscle != null && armFlexionMuscle != null && forearmExtensionMuscle != null && forearmFlexionMuscle != null)
            {
                HighlightRightArm();
                VisualizeMuscleActivation();
            }

            
        }

        public void ActivateMotion()
        {
            // Hips와 모든 하위 오브젝트를 활성화
            //SetGameObjectActive(Hips, true);

            // Load and visualize the motion
            string fileName = $"{subject}_{strokeType}_{strokeNumber}.csv";

            ReadCSV();
            Normalization();

            Debug.Log("Expert Motion Activated.");

            StartMotion();
        }

        public void ClearMotion()
        {
            // Hips와 모든 하위 오브젝트를 비활성화
            //SetGameObjectActive(Hips, false);


            Debug.Log("Motion cleared.");
        }

        // Hips와 모든 하위 오브젝트 활성화 또는 비활성화
        private void SetGameObjectActive(GameObject root, bool isActive)
        {
            if (root == null) return;

            root.SetActive(isActive);

            foreach (Transform child in root.transform)
            {
                child.gameObject.SetActive(isActive);
            }
        }

        private bool ReadCSV()
        {
            string fileName = $"{subject}_{strokeType}_{strokeNumber}.csv";

            string fullPath = Path.Combine(folderPath, fileName);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"File not found: {fullPath}");
                return false;
            }

            jointMuscle.Clear();

            string[] lines = File.ReadAllLines(fullPath);

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;

                string[] parts = lines[i].Split(',');

                Quaternion[] rotations = new Quaternion[21];
				Vector3[] positions = new Vector3[21];
                float[] muscle = new float[4];


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
                    muscle[0] = float.TryParse(parts[0], out float parsedX) ? parsedX : 0f;
                    muscle[1] = float.TryParse(parts[4], out float parsedY) ? parsedY : 0f;
                    muscle[2] = float.TryParse(parts[8], out float parsedZ) ? parsedZ : 0f;
                    muscle[3] = float.TryParse(parts[12], out float parsedW) ? parsedW : 0f;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing EMG data at frame {i}. Exception: {ex.Message}");
                    muscle = new float[4]; // Default to zero values if parsing fails
                }

                jointPositionData[i] = positions;
                jointRotationData[i] = rotations;
                ArmMuscle[i] = muscle;
                Debug.Log(muscle);
                Debug.Log(parts[0]);
            }

            Debug.Log("joint Data Number : " + jointRotationData.Count.ToString());
            Debug.Log("EMG Data Number : " + ArmMuscle.Count.ToString());
            return true;
        }

        private void Normalization()
        {
            Vector3 standard = jointPositionData[0][0];

            for (int i=0; i<jointPositionData.Count; i++)
            {
                for (int j=0; j < jointPositionData[i].Length; j++)
                {
                    jointPositionData[i][j] /= 100;
                }
            }
        }

        /*new void Update()
		{
			//base.ToggleConnect();
			base.Update();

			if (boundActor != null && boundTransforms && motionUpdateMethod == UpdateMethod.Normal && isTriggered)
			{
				if (physicalReference.Initiated())
				{
					ReleasePhysicalContext();
				}

                StartCoroutine(ApplyMotionCSV(transforms, bonePositionOffsets, boneRotationOffsets, enableHipMove, orignalRot, orignalParentRot, orignalPositions));
				Debug.Log("update started");
			}
		}*/

        public void StartMotion()
        {
            //base.ToggleConnect();
            // base.Update();

            if (boundTransforms && motionUpdateMethod == UpdateMethod.Normal)
            {
                if (physicalReference.Initiated())
                {
                    ReleasePhysicalContext();
                }

                Debug.Log("StartMotion function has been launched");
                StartCoroutine(ApplyMotionCSV(transforms, bonePositionOffsets, boneRotationOffsets, enableHipMove, orignalRot, orignalParentRot, orignalPositions));
                
            }
        }

        /*void FixedUpdate()
		{
			//base.ToggleConnect();
			
			if(boundTransforms && motionUpdateMethod != UpdateMethod.Normal )
			{				
				if( !physicalReference.Initiated() )
				{
					InitPhysicalContext();
				}

				ApplyMotionPhysically( physicalReference.GetReferenceTransforms(), transforms );
			}
		}*/
		
		public Transform[] GetTransforms()
		{
			return transforms;
		}

        public void LoadExpertPoseData()
        {
            string fileName = $"{subject}_{strokeType}_{strokeNumber}.csv";
            string filePath = Path.Combine(folderPath, fileName);

            if (!File.Exists(filePath))
            {
                Debug.LogError($"Expert pose data file not found: {filePath}");
                return;
            }

            expertPoseData.Clear();

            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] values = line.Split(',');
                        Vector3[] pose = new Vector3[values.Length / 3];

                        // Normalize all positions relative to the first joint (e.g., hip joint)
                        float hipX = float.Parse(values[0]) / 100;
                        float hipY = float.Parse(values[1]) / 100;
                        float hipZ = float.Parse(values[2]) / 100;

                        for (int i = 0; i < pose.Length; i++)
                        {
                            float x = float.Parse(values[i * 3]) / 100 - hipX;
                            float y = float.Parse(values[i * 3 + 1]) / 100 - hipY;
                            float z = float.Parse(values[i * 3 + 2]) / 100 - hipZ;
                            pose[i] = new Vector3(x, y, -z); // Flip Z-axis for Unity coordinate system
                        }

                        expertPoseData.Add(pose);
                    }
                }

                Debug.Log($"Expert pose data loaded from {filePath}. Total frames: {expertPoseData.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading expert pose data: {e.Message}");
            }
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

		public IEnumerator ApplyMotionCSV(Transform[] transforms, Vector3[] bonePositionOffsets, Vector3[] boneRotationOffsets, bool enableHipMove, Quaternion[] orignalRot, Quaternion[] orignalParentRot, Vector3[] orignalPositions)
		{
            int count = 0;
            bool isFirstFrame = true; // 첫 프레임 체크용 플래그


            while (count < jointRotationData.Count)
			{
       
                Quaternion[] currentRotation = jointRotationData[count];
                Vector3[] currentPosition = jointPositionData[count];

                // apply Hips position
                //if (enableHipMove && (!disableBoneMovement[(int)NeuronBones_simple.Hips]))
                //{
                    //Vector3 tempPosition = currentPosition[(int)NeuronBones_simple.Hips] != null ? currentPosition[(int)NeuronBones_simple.Hips] : orignalPositions[(int)NeuronBones_simple.Hips];
                    //SetPosition(transforms, NeuronBones_simple.Hips, tempPosition);
                    //SetPosition(transforms, NeuronBones_simple.Hips, Vector3.zero);
                    //}

                    //else
                    //{
                    //Vector3 p = currentPosition[(int)NeuronBones_simple.Hips];
                    //SetPosition(transforms, NeuronBones_simple.Hips, new Vector3(0f, p.y, 0f));
                    //Vector3 p = currentPosition[(int)NeuronBones_simple.Hips];
                    //SetPosition(transforms, NeuronBones_simple.Hips, new Vector3(0f, p.y, 0f));
                    //}

                    // Hip 위치 설정
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
                //if (actor.AvatarWithDisplacement )
                {
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
                        Vector3 rot = currentRotation[i].eulerAngles + boneRotationOffsets[i];
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
                            Vector3 srcP = currentPosition[i] + bonePositionOffsets[i];
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

                    // 정규화 (MVC 기반)
                    float normForearmFlexion = NormalizeEMGValue(ArmMuscle[count][0], mvcValue);
                    float normForearmExtension = NormalizeEMGValue(ArmMuscle[count][1], mvcValue);
                    float normArmFlexion = NormalizeEMGValue(ArmMuscle[count][2], mvcValue);
                    float normArmExtension = NormalizeEMGValue(ArmMuscle[count][3], mvcValue);

                    Debug.Log("normForearmFlexion" + normForearmFlexion);
                    Debug.Log("normForearmExtension" + normForearmExtension);
                    Debug.Log("normArmFlexion" + normArmFlexion);
                    Debug.Log("normArmExtension" + normArmExtension);

                    // EMA 적용
                    forearmFlexionPrevEMA = ApplyExponentialMovingAverage(normForearmFlexion, forearmFlexionPrevEMA, alpha);
                    forearmExtensionPrevEMA = ApplyExponentialMovingAverage(normForearmExtension, forearmExtensionPrevEMA, alpha);
                    armFlexionPrevEMA = ApplyExponentialMovingAverage(normArmFlexion, armFlexionPrevEMA, alpha);
                    armExtensionPrevEMA = ApplyExponentialMovingAverage(normArmExtension, armExtensionPrevEMA, alpha);

                    // Threshold Damping 적용
                    forearmFlexionMuscleActivation = ApplyThresholdDamping(forearmFlexionPrevEMA, forearmFlexionMuscleActivation, threshold);
                    forearmExtensionMuscleActivation = ApplyThresholdDamping(forearmExtensionPrevEMA, forearmExtensionMuscleActivation, threshold);
                    armFlexionMuscleActivation = ApplyThresholdDamping(armFlexionPrevEMA, armFlexionMuscleActivation, threshold);
                    armExtensionMuscleActivation = ApplyThresholdDamping(armExtensionPrevEMA, armExtensionMuscleActivation, threshold);

                    // 시각화 적용 (Optional)
                    VisualizeMuscleActivation();
                    

                    yield return new WaitForSeconds(1/(30*speed));
                    count++;
                }
            }
		}

		// apply transforms extracted from actor mocap data to bones
		/*public void ApplyMotion( NeuronActor actor, Transform[] transforms, Vector3[] bonePositionOffsets, Vector3[] boneRotationOffsets , bool enableHipMove,  Quaternion[] orignalRot , Quaternion[] orignalParentRot , Vector3[] orignalPositions)
		{
            if (!actor.HsReceivedData)
                return;
            // apply Hips position
            if (enableHipMove &&  (!disableBoneMovement[(int)NeuronBones_simple.Hips]))
                SetPosition(transforms, NeuronBones_simple.Hips, actor.GetReceivedPosition(NeuronBones.Hips, orignalPositions[(int)NeuronBones.Hips]));
            else
            {
                Vector3 p = actor.GetReceivedPosition(NeuronBones.Hips);
                SetPosition(transforms, NeuronBones_simple.Hips, new Vector3(0f, p.y, 0f));
            }

            SetRotation(transforms, NeuronBones_simple.Hips,
                (Quaternion.Euler(actor.GetReceivedRotation(NeuronBones.Hips)) * orignalRot[(int)NeuronBones.Hips]).eulerAngles);


            // apply positions
            //if (actor.AvatarWithDisplacement )
			{
				for( int i = 1; i < (int)NeuronBones.NumOfBones && i < transforms.Length; ++i )
				{
                    if (transforms[i] == null)
                        continue;

                    // q
                    Quaternion orignalBoneRot = Quaternion.identity;
                    if (orignalRot != null)
                    {
                        orignalBoneRot = orignalRot[i];
                    }
                    Vector3 rot = actor.GetReceivedRotation((NeuronBones)i) + boneRotationOffsets[i] ;
                    //Debug.LogError(actor.AvatarIndex + " " +  actor.GetReceivedRotation((NeuronBones)i) + ", " + rot);
                    Quaternion srcQ = Quaternion.Euler(rot);

                    Quaternion usedQ = Quaternion.Inverse(orignalParentRot[i]) * srcQ * orignalParentRot[i];
                    Vector3 transedRot = usedQ.eulerAngles;
                    Quaternion finalBoneQ = Quaternion.Euler(transedRot) * orignalBoneRot;
                    SetRotation(transforms, (NeuronBones_simple)i, finalBoneQ.eulerAngles);

                    // p   
                    bool enableNodeMove = actor.GetHasPosition((NeuronBones)i);
                    enableNodeMove &= (!disableBoneMovement[i]);
                    if (!enableFingerMove)
                    {
                        if (i >= (int)NeuronBones.RightHand && i <= (int)NeuronBones.RightHandPinky3)
                            enableNodeMove = false;
                        if (i >= (int)NeuronBones.LeftHand && i <= (int)NeuronBones.LeftHandPinky3)
                            enableNodeMove = false;
                    }
                    if (enableNodeMove)
                    {
                        Vector3 srcP = actor.GetReceivedPosition((NeuronBones)i) + bonePositionOffsets[i];
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
            }
			
		}*/
		
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

		
		public bool Bind( Transform root, string prefix )
		{
			this.root = root;
			this.prefix = prefix;
			//int bound_count = 
            NeuronHelper.Bind( root, transforms, prefix, false, (skeletonType == NeuronEnums.SkeletonType.PerceptionNeuronStudio) ? NeuronBoneVersion.V1 : NeuronBoneVersion.V2);
            boundTransforms = true; // bound_count >= (int)NeuronBones.NumOfBones;
			UpdateOffset();
            CaluateOrignalRot();
            return boundTransforms;
		}
		
		void InitPhysicalContext()
		{
			if( physicalReference.Init( root, prefix, transforms, physicalReferenceOverride ) )
			{
				// break original object's hierachy of transforms, so we can use MovePosition() and MoveRotation() to set transform
				NeuronHelper.BreakHierarchy( transforms );
			}

			CheckRigidbodySettings ();
		}

		
		void ReleasePhysicalContext()
		{
			physicalReference.Release();
		}
		
		void UpdateOffset()
		{
			// initiate values
			for( int i = 0; i < (int)NeuronBones_simple.NumOfBones; ++i )
			{
				bonePositionOffsets[i] = Vector3.zero;
				boneRotationOffsets[i] = Vector3.zero;
			}
			/*
			if( boundTransforms )
			{
                if(transforms[(int)NeuronBones.LeftUpLeg] != null)
				    bonePositionOffsets[(int)NeuronBones.LeftUpLeg] = new Vector3( 0.0f, transforms[(int)NeuronBones.LeftUpLeg].localPosition.y, 0.0f );
                if(transforms[(int)NeuronBones.RightUpLeg] != null)
				    bonePositionOffsets[(int)NeuronBones.RightUpLeg] = new Vector3( 0.0f, transforms[(int)NeuronBones.RightUpLeg].localPosition.y, 0.0f );
			}
			*/
		}

        void CaluateOrignalRot()
        {
            for (int i = 0; i < orignalPositions.Length; i++)
            {
                orignalPositions[i] = transforms[i] == null ? Vector3.zero : transforms[i].localPosition;
            }
            for (int i = 0; i < orignalRot.Length; i++)
            {
                orignalRot[i] = transforms[i] == null ? Quaternion.identity : transforms[i].localRotation;
            }
            for (int i = 0; i < orignalRot.Length; i++)
            {
                Quaternion parentQs = Quaternion.identity;
                if (transforms[i] == null)
                {
                    orignalParentRot[i] = Quaternion.identity;
                    continue;
                }
                Transform tempParent = transforms[i].transform.parent;
                while (tempParent != null)
                {
                    parentQs = tempParent.transform.localRotation * parentQs;
                    tempParent = tempParent.parent;
                    if (tempParent == null || tempParent == this.transform || (transforms[0] != null && tempParent == transforms[0].transform.parent))
                        break;
                }
                orignalParentRot[i] = parentQs;
            }
        }
		void CheckRigidbodySettings( ){
			//check if rigidbodies have correct settings
			bool kinematicSetting = false;
			if (motionUpdateMethod == UpdateMethod.Physical) {
				kinematicSetting = true;
			}

			for( int i = 0; i < (int)NeuronBones_simple.NumOfBones && i < transforms.Length; ++i )
			{
				Rigidbody r = transforms[i].GetComponent<Rigidbody> ();
				if (r != null) {
					r.isKinematic = kinematicSetting;
				}
			}
		}

        private void UpdateMuscleActivations(float[] emgValues, float mvcValue, float alpha = 0.1f, float threshold = 0.05f)
        {
            if (emgValues.Length >= 4)
            {
                // 정규화 (MVC 기반)
                float normForearmFlexion = NormalizeEMGValue(emgValues[0], mvcValue);
                float normForearmExtension = NormalizeEMGValue(emgValues[1], mvcValue);
                float normArmFlexion = NormalizeEMGValue(emgValues[2], mvcValue);
                float normArmExtension = NormalizeEMGValue(emgValues[3], mvcValue);

                // EMA 적용
                forearmFlexionPrevEMA = ApplyExponentialMovingAverage(normForearmFlexion, forearmFlexionPrevEMA, alpha);
                forearmExtensionPrevEMA = ApplyExponentialMovingAverage(normForearmExtension, forearmExtensionPrevEMA, alpha);
                armFlexionPrevEMA = ApplyExponentialMovingAverage(normArmFlexion, armFlexionPrevEMA, alpha);
                armExtensionPrevEMA = ApplyExponentialMovingAverage(normArmExtension, armExtensionPrevEMA, alpha);

                // Threshold Damping 적용
                forearmFlexionMuscleActivation = ApplyThresholdDamping(forearmFlexionPrevEMA, forearmFlexionMuscleActivation, threshold);
                forearmExtensionMuscleActivation = ApplyThresholdDamping(forearmExtensionPrevEMA, forearmExtensionMuscleActivation, threshold);
                armFlexionMuscleActivation = ApplyThresholdDamping(armFlexionPrevEMA, armFlexionMuscleActivation, threshold);
                armExtensionMuscleActivation = ApplyThresholdDamping(armExtensionPrevEMA, armExtensionMuscleActivation, threshold);

                // 시각화 적용 (Optional)
                VisualizeMuscleActivation();
            }
        }

        // 오른쪽 팔 하이라이트
        void HighlightRightArm()
        {
            ApplyMaterialToTransform(armFlexionMuscle.transform, highlightMaterial);
            ApplyMaterialToTransform(armExtensionMuscle.transform, highlightMaterial);
            ApplyMaterialToTransform(forearmFlexionMuscle.transform, highlightMaterial);
            ApplyMaterialToTransform(forearmExtensionMuscle.transform, highlightMaterial);
        }

        // 근육 활성도 시각화
        void VisualizeMuscleActivation()
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
