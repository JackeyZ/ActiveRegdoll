#define RAGDOLLCONTROL
#define SIMPLEFOOTIK
using UnityEngine;
using System.Collections;

namespace AnimFollow
{
	/// <summary>
	/// 挂载到布娃娃根节点
	/// 该脚本用于融合布娃娃节点与主动画节点的位置和旋转
	/// </summary>
	public class AnimFollow_AF : MonoBehaviour
	{
		public readonly int version = 7; // The version of this script

		// Variables (expand #region by clicking the plus)
#region

#if RAGDOLLCONTROL
		RagdollControl_AF ragdollControl;
#endif

		public GameObject master;										// 主动画游戏对象（要手动拖进去）
		public Transform[] masterTransforms;                            // 主动画游戏对象所有对应布娃娃的非刚体子节点（自动生成）
		public Transform[] masterRigidTransforms = new Transform[1];    // 主动画游戏对象下对应布娃娃刚体的骨骼（自动生成）
		public Transform[] slaveTransforms;                             // 储存布娃娃下所有非刚体子节点（自动生成）
		public Rigidbody[] slaveRigidbodies = new Rigidbody[1];         // 储存布娃娃下所有刚体组件（自动生成）
		public Vector3[] rigidbodiesPosToCOM;
		public Transform[] slaveRigidTransforms = new Transform[1];     // 布娃娃下所有刚体节点
		public Transform[] slaveExcludeTransforms;						// 布娃娃下排除在外的节点（需要手动拖进去）

		Quaternion[] localRotations1 = new Quaternion[1];				// 存储布娃娃下非刚体节点的本地旋转
		Quaternion[] localRotations2 = new Quaternion[1];				// 一般情况下储存主动画对象下的非刚体节点的本地旋转

		public float fixedDeltaTime = 0.01f;	// If you choose to go to longer times you need to lower PTorque, PLocalTorque and PForce or the system gets unstable. Can be done, longer time is better performance but worse mimicking of master.
		float reciFixedDeltaTime;				// 1f / fixedDeltaTime
		         
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		
		// The ranges are not set in stone. Feel free to extend the ranges
		[Range(0f, 100f)] public float maxTorque = 100f; // Limits the world space torque
		[Range(0f, 100f)] public float maxForce = 100f; // Limits the force
		[Range(0f, 10000f)] public float maxJointTorque = 10000f; // RagdollControl_AF脚本设过来的 Limits the force
		[Range(0f, 10f)] public float jointDamping = .6f; // 关节阻尼

		public float[] maxTorqueProfile = {100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f}; // 每个关节的限制，Individual limits per limb
		public float[] maxForceProfile = {1f, .2f, .2f, .2f, .2f, 1f, 1f, .2f, .2f, .2f, .2f, .2f};
		public float[] maxJointTorqueProfile = {1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f};
		public float[] jointDampingProfile = {1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f};

		// pd控制器里p项的常量系数A(分两种：扭矩和力）
		[Range(0f, .64f)] public float PTorque = .16f; 
		[Range(0f, 160f)] public float PForce = 30f;

		// pd控制器里d项的常量
		[Range(0f, .008f)] public float DTorque = .002f; 
		[Range(0f, .064f)] public float DForce = .01f;

		// pd控制器里p项的常量系数2，不同关节读取不同的数值
		//	public float[] PTorqueProfile = {20f, 30f, 10f, 30f, 10f, 30f, 30f, 30f, 10f, 30f, 10f}; // Per limb world space torque strength
		public float[] PTorqueProfile = {20f, 30f, 10f, 30f, 10f, 30f, 30f, 30f, 30f, 10f, 30f, 10f}; // Per limb world space torque strength for EthanRagdoll_12 (twelve rigidbodies)
		public float[] PForceProfile = {1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f};
		
		// The ranges are not set in stone. Feel free to extend the ranges
		[Range(0f, 340f)] public float angularDrag = 100f;		// 刚体的角阻力，用于阻碍刚体旋转
		[Range(0f, 2f)] public float drag = .5f;				// 刚体阻力
		float maxAngularVelocity = 1000f;						// 刚体最大角速度
		
		[SerializeField] bool torque = false;					// 用世界空间的扭矩取控制布娃娃 (if true)
		[SerializeField] bool force = true;						// 用力去控制布娃娃
		[HideInInspector] public bool mimicNonRigids = true;    // 设置所有非刚体节点的本地旋转以匹配主动画对象的本地旋转 Set all local rotations of the transforms without rigidbodies to match the local rotations of the master
		[HideInInspector] [Range(2, 100)] public int secondaryUpdate = 2;
		int frameCounter;
		public bool hideMaster = true;
		public bool useGravity = true;							// 关节刚体是否应用unity自带的重力
		bool userNeedsToAssignStuff = false;
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		
		float torqueAngle; // Återanvänds för localTorque, därför ingen variabel localTorqueAngle
		Vector3 torqueAxis;
		Vector3 torqueError;
		Vector3 torqueSignal;
		Vector3[] torqueLastError = new Vector3[1];
		Vector3 torqueVelError;
		[HideInInspector] public Vector3 totalTorqueError; // 世界空间下所有关节的角度误差，这是一个向量 Total world space angular error of all limbs. This is a vector.
		
		Vector3 forceAxis;
		Vector3 forceSignal;
		Vector3 forceError;
		Vector3[] forceLastError = new Vector3[1];
		Vector3 forceVelError;
		[HideInInspector] public Vector3 totalForceError; // 世界空间下的位置误差， 这是一个向量
		public float[] forceErrorWeightProfile = {1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f}; // Per limb error weight
		
		float masterAngVel;
		Vector3 masterAngVelAxis;
		float slaveAngVel;
		Vector3 slaveAngVelAxis;
		Quaternion masterDeltaRotation;
		Quaternion slaveDeltaRotation;
		Quaternion[] lastMasterRotation = new Quaternion[1];
		Quaternion[] lastSlaveRotation = new Quaternion[1];
		Quaternion[] lastSlavelocalRotation = new Quaternion[1];
		Vector3[] lastMasterPosition = new Vector3[1];
		Vector3[] lastSlavePosition = new Vector3[1];
		
		Quaternion[] startLocalRotation = new Quaternion[1];						// 储存布娃娃下刚体节点在关节空间下的初始旋转值
		ConfigurableJoint[] configurableJoints = new ConfigurableJoint[1];			// 储存布娃娃下所有的关节组件
		Quaternion[] localToJointSpace = new Quaternion[1];                         // 储存本地空间到关节空间的转换旋转值
		JointDrive jointDrive = new JointDrive();
#endregion

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void Awake() // Initialize
		{
			Time.fixedDeltaTime = fixedDeltaTime; // 设置物理系统的update间隔
	//		Debug.Log("The script AnimFollow has set the fixedDeltaTime to " + fixedDeltaTime); // Remove this line if you don't need the "heads up"
			reciFixedDeltaTime = 1f / fixedDeltaTime; // 缓存一个倒数
			
			if (!master)
			{
				UnityEngine.Debug.LogWarning("master not assigned in AnimFollow script on " + this.name + "\n");
				userNeedsToAssignStuff = true;
				return;
			}
#if SIMPLEFOOTIK
			else if (!master.GetComponent<SimpleFootIK_AF>())
			{
				UnityEngine.Debug.LogWarning("Missing script SimpleFootIK_AF on " + master.name + ".\nAdd it or comment out the directive from top line in the AnimFollow script." + "\n");
				userNeedsToAssignStuff = true;
			}
#else
			else if (master.GetComponent<SimpleFootIK_AF>())
			{
				UnityEngine.Debug.LogWarning("There is a SimpleFootIK script on\n" + master.name + " But the directive in the AnimFollow script is commented out");
				userNeedsToAssignStuff = true;
			}
#endif
			else if (hideMaster)
			{
				SkinnedMeshRenderer visible;
				MeshRenderer visible2;
				if (visible = master.GetComponentInChildren<SkinnedMeshRenderer>())
				{
					visible.enabled = false;
					SkinnedMeshRenderer[] visibles;
					visibles = master.GetComponentsInChildren<SkinnedMeshRenderer>();
					foreach (SkinnedMeshRenderer visiblen in visibles)
						visiblen.enabled = false;
				}
				if (visible2 = master.GetComponentInChildren<MeshRenderer>())
				{
					visible2.enabled = false;
					MeshRenderer[] visibles2;
					visibles2 = master.GetComponentsInChildren<MeshRenderer>();
					foreach (MeshRenderer visiblen2 in visibles2)
						visiblen2.enabled = false;
				}
			}
			#if RAGDOLLCONTROL
			if (!(ragdollControl = GetComponent<RagdollControl_AF>()))
			{
				UnityEngine.Debug.LogWarning("Missing script RagdollControl on " + this.name + ".\nAdd it or comment out the directive from top line in the AnimFollow script." + "\n");
				userNeedsToAssignStuff = true;
			}
			#else
			if (GetComponent<RagdollControl_AF>())
			{
				UnityEngine.Debug.LogWarning("There is a RagdollControl script on\n" + this.name + " But the directive in the AnimFollow script is commented out");
				userNeedsToAssignStuff = true;
			}
			#endif

			slaveTransforms = GetComponentsInChildren<Transform>();			// 获取布娃娃游戏对象下的所有子节点，必须和主动画对象下的子节点一致，初始化之后仅储存非刚体节点
			masterTransforms = master.GetComponentsInChildren<Transform>(); // 获取主动画游戏对象下的所有子节点
			System.Array.Resize(ref localRotations1, slaveTransforms.Length);
			System.Array.Resize(ref localRotations2, slaveTransforms.Length);
			System.Array.Resize(ref rigidbodiesPosToCOM, slaveTransforms.Length);

			// 检查子节点数目是否对应
			if (!(masterTransforms.Length == slaveTransforms.Length))
			{
				UnityEngine.Debug.LogWarning(this.name + " does not have a valid master.\nMaster transform count does not equal slave transform count." + "\n");
				userNeedsToAssignStuff = true;
				return;
			}

			// Resize Arrays (expand #region)
			#region
			slaveRigidbodies = GetComponentsInChildren<Rigidbody>();
			int rigidbodyCount = slaveRigidbodies.Length;                                    // 布娃娃刚体骨骼数目
			System.Array.Resize(ref masterRigidTransforms, rigidbodyCount);
			System.Array.Resize(ref slaveRigidTransforms, rigidbodyCount);

			System.Array.Resize(ref maxTorqueProfile, rigidbodyCount);
			System.Array.Resize(ref maxForceProfile, rigidbodyCount);
			System.Array.Resize(ref maxJointTorqueProfile, rigidbodyCount);
			System.Array.Resize(ref jointDampingProfile, rigidbodyCount);
			System.Array.Resize(ref PTorqueProfile, rigidbodyCount);
			System.Array.Resize(ref PForceProfile, rigidbodyCount);
			System.Array.Resize(ref forceErrorWeightProfile, rigidbodyCount);
			
			System.Array.Resize(ref torqueLastError, rigidbodyCount);
			System.Array.Resize(ref forceLastError, rigidbodyCount);
			
			System.Array.Resize(ref lastMasterRotation, rigidbodyCount);
			System.Array.Resize(ref lastSlaveRotation, rigidbodyCount);
			System.Array.Resize(ref lastSlavelocalRotation, rigidbodyCount);
			System.Array.Resize(ref lastMasterPosition, rigidbodyCount);
			System.Array.Resize(ref lastSlavePosition, rigidbodyCount);
			
			System.Array.Resize(ref startLocalRotation, rigidbodyCount);
			System.Array.Resize(ref configurableJoints, rigidbodyCount);
			System.Array.Resize(ref localToJointSpace, rigidbodyCount);
			#endregion

			//			int j = 0;
			//			foreach (Transform ragdollRigidTransform in ragdollRigidTransforms) // Set up configurable joints and rigidbodies
			int rigidIndex = 0;					// 刚体节点index
			int normalTransIndex = 0;           // 普通节点index
			int transIndex = 0;                 // 所有节点index
			int jointCount = 0;                 // 刚体关节数目

			// 遍历布娃娃下的所有节点
			foreach (Transform slaveTransform in slaveTransforms) // Sort the transform arrays
			{	
				// 判断是否有刚体组件
				if (slaveTransform.GetComponent<Rigidbody>())
				{
					slaveRigidTransforms[rigidIndex] = slaveTransform;
					masterRigidTransforms[rigidIndex] = masterTransforms[transIndex];
					// 判断是否有关节组件（必须要有）
					if (slaveTransform.GetComponent<ConfigurableJoint>())
					{
						configurableJoints[rigidIndex] = slaveTransform.GetComponent<ConfigurableJoint>();
						Vector3 forward = Vector3.Cross(configurableJoints[rigidIndex].axis, configurableJoints[rigidIndex].secondaryAxis);  // axis是一个世界空间下的向量，定义了关节空间的x基底，secondaryAxis则是y基底，叉乘算出z的基底
						Vector3 up = configurableJoints[rigidIndex].secondaryAxis;
						localToJointSpace[rigidIndex] = Quaternion.LookRotation(forward, up);                                       // 得出一个本地空间到关节空间的转换旋转值，z轴方向（forward向量）对应的旋转值（如果把该值赋值给某个物体的rotation，那么该物体的z轴则会与这里的forward变成同一方向）
						startLocalRotation[rigidIndex] = slaveTransform.localRotation * localToJointSpace[rigidIndex];              // 算出布娃娃一开始的本地旋转值，并转换到关节空间
						jointDrive = configurableJoints[rigidIndex].slerpDrive;														// 取出旋转驱动器，存起来
						//jointDrive.mode = JointDriveMode.Position;
						//configurableJoints[j].slerpDrive = jointDrive;
						jointCount++;																								// 关节数目
					}
					else if (rigidIndex > 0)
					{
						UnityEngine.Debug.LogWarning("Rigidbody " + slaveTransform.name + " on " + this.name + " is not connected to a configurable joint" + "\n");
						userNeedsToAssignStuff = true;
						return;
					}
					// worldCenterOfMass是刚体的质心在世界空间下的坐标
					// 算出来的rigidbodiesPosToCOM是刚体质心在slaveTransform下的本地坐标，貌似等价于slaveTransform.GetComponent<Rigidbody>().centerOfMass
					rigidbodiesPosToCOM[rigidIndex] = Quaternion.Inverse(slaveTransform.rotation) * (slaveTransform.GetComponent<Rigidbody>().worldCenterOfMass - slaveTransform.position);
					//Debug.Log("---" + rigidbodiesPosToCOM[rigidIndex]);
					//Debug.Log(slaveTransform.GetComponent<Rigidbody>().centerOfMass);
					rigidIndex++;
				}
				else
				{
					// 判断是否属于排除在外的节点
					bool excludeBool = false;
					foreach (Transform exclude in slaveExcludeTransforms)
					{
						if (slaveTransform == exclude)
						{
							excludeBool = true;
							break;
						}
					}

					if (!excludeBool)
					{
						slaveTransforms[normalTransIndex] = slaveTransform;
						masterTransforms[normalTransIndex] = masterTransforms[transIndex];
						localRotations1[normalTransIndex] = slaveTransform.localRotation;
						normalTransIndex++;
					}
				}
				transIndex++;
			}
			localRotations2 = localRotations1;      // localRotations2初始数值与localRotations1一致
			System.Array.Resize(ref masterTransforms, normalTransIndex);
			System.Array.Resize(ref slaveTransforms, normalTransIndex);
			System.Array.Resize(ref localRotations1, normalTransIndex);
			System.Array.Resize(ref localRotations2, normalTransIndex);
			
			if (jointCount == 0)
			{
				UnityEngine.Debug.LogWarning("There are no configurable joints on the ragdoll " + this.name + "\nDrag and drop the ReplaceJoints script on the ragdoll." + "\n");
				userNeedsToAssignStuff = true;
				return;
			}
			else
			{
				SetJointTorque (maxJointTorque);
				EnableJointLimits(false);
			}

			if (slaveRigidTransforms.Length == 0 )
				UnityEngine.Debug.LogWarning("There are no rigid body components on the ragdoll " + this.name + "\n");
			else if (slaveRigidTransforms.Length < 12)
				UnityEngine.Debug.Log("This version of AnimFollow works better with one extra colleder in the spine on " + this.name + "\n");

			if (PTorqueProfile[PTorqueProfile.Length - 1] == 0f)
				UnityEngine.Debug.Log("The last entry in the PTorqueProfile is zero on " + this.name +".\nIs that intentional?\nDrop ResizeProfiles on the ragdoll and adjust the values." + "\n");

			if (slaveExcludeTransforms.Length == 0)
			{
				UnityEngine.Debug.Log("Should you not assign some slaveExcludeTransforms to the AnimFollow script on " + this.name + "\n");
			}
		}

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void Start()
		{	
			if (userNeedsToAssignStuff)
				return;
			
			// 给刚体组件设置参数
			int i = 0;
			foreach(Transform slaveRigidTransform in slaveRigidTransforms) // Set some of the Unity parameters
			{
				slaveRigidTransform.GetComponent<Rigidbody>().useGravity = useGravity;
				slaveRigidTransform.GetComponent<Rigidbody>().angularDrag = angularDrag;
				slaveRigidTransform.GetComponent<Rigidbody>().drag = drag;
				slaveRigidTransform.GetComponent<Rigidbody>().maxAngularVelocity = maxAngularVelocity;
				i++;
			}
		}

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#if RAGDOLLCONTROL && !SIMPLEFOOTIK
		void FixedUpdate ()
		{
			DoAnimFollow();
		}
#endif

		public void DoAnimFollow()
		{
			if (userNeedsToAssignStuff)
				return;
			
#if RAGDOLLCONTROL
			ragdollControl.DoRagdollControl();
			if (ragdollControl.stayDeadOnHeadShot && ragdollControl.shotInHead)	// 如果死亡，则不进行动画跟随
				return;
#endif

			totalTorqueError = Vector3.zero;
			totalForceError = Vector3.zero;

			if (frameCounter % secondaryUpdate == 0)
			{
				if (mimicNonRigids) 
				{
					// 把主动画对象的非刚体节点的本地旋转储存到localRotations2上
					for (int i = 2; i < slaveTransforms.Length - 1; i++) 
					{
						localRotations2[i] = masterTransforms[i].localRotation;
					}
				}
				// 设置扭矩和阻尼
				SetJointTorque(maxJointTorque, jointDamping);
			}

			/*********************非刚体节点的旋转融合*********************/
			// 每两帧执行一次
			if (frameCounter % 2 == 0)
			{
				// 把主动画对象非刚体节点的本地旋转插值给布娃娃节点下的非刚体节点
				for (int i = 2; i < slaveTransforms.Length - 1; i++) // Set all local rotations of the transforms without rigidbodies to match the local rotations of the master
				{
					// 分帧大于2的时候才做插值，否则直接赋值
					// secondaryUpdate越大，融合的时长越长
					if (secondaryUpdate > 2)
					{
						localRotations1[i] = Quaternion.Lerp(localRotations1[i], localRotations2[i], 2f / secondaryUpdate);
						slaveTransforms[i].localRotation = localRotations1[i];
					}
                    else
					{
						slaveTransforms[i].localRotation = localRotations2[i];
					}
				}
			}

			/*********************刚体节点的位置和旋转融合*********************/
			// 遍历布娃娃下的所有刚体节点
			for (int i = 0; i < slaveRigidTransforms.Length; i++) // Do for all rigid bodies
			{
				// 实时设置阻力
				slaveRigidbodies[i].angularDrag = angularDrag;
				slaveRigidbodies[i].drag = drag;

				Quaternion targetRotation;
				// 计算和应用世界空间下的扭矩（torque暂时是false，没用上）
				if (torque)
				{
					targetRotation = masterRigidTransforms[i].rotation * Quaternion.Inverse(slaveRigidTransforms[i].rotation);
					targetRotation.ToAngleAxis(out torqueAngle, out torqueAxis);
					torqueError = FixEuler(torqueAngle) * torqueAxis;

					if(torqueAngle != 360f)
					{
						totalTorqueError += torqueError;
						PDControl (PTorque * PTorqueProfile[i], DTorque, out torqueSignal, torqueError, ref torqueLastError[i], reciFixedDeltaTime);
					}
                    else
					{
						torqueSignal = new Vector3(0f, 0f, 0f);
					}

					torqueSignal = Vector3.ClampMagnitude(torqueSignal, maxTorque * maxTorqueProfile[i]);		// 把向量限制在特定的长度
					slaveRigidbodies[i].AddTorque(torqueSignal, ForceMode.VelocityChange);						// 添加一个扭矩给关节
				}

				/**********************用于混合位置***********************/
				// rigidbodiesPosToCOM是布娃娃下刚体质心的本地坐标
				// 算出来的masterRigidTransformsWCOM则是理论上主动画对象下刚体节点的质心的世界坐标
				Vector3 masterRigidTransformsWCOM = masterRigidTransforms[i].position + masterRigidTransforms[i].rotation * rigidbodiesPosToCOM[i];
				// 力误差， 布娃娃实际上的质心和理论上的质心位置的差异
				forceError = masterRigidTransformsWCOM - slaveRigidTransforms[i].GetComponent<Rigidbody>().worldCenterOfMass; // Doesn't work if collider is trigger
				totalForceError += forceError * forceErrorWeightProfile[i];										// 乘一个四肢权重
				
				if (force) // Calculate and apply world force
				{
					// pd控制器计算出需要修正的力（当关节越偏离正常方向，则算出来的力越大，用于修正关节的偏离）
					PDControl (PForce * PForceProfile[i], DForce, out forceSignal, forceError, ref forceLastError[i], reciFixedDeltaTime);
					forceSignal = Vector3.ClampMagnitude(forceSignal, maxForce * maxForceProfile[i]);   // 力的大小做一个限制，maxForce是RagdollControl设置的
					// 给刚体添加修正的力，让刚体跟随主动画对象的位置
					slaveRigidbodies[i].AddForce(forceSignal, ForceMode.VelocityChange);
				}

				/**********************用于混合旋转***********************/
				// 排除掉根节点的刚体（因为根节点是没有关节组件的）
				if (i > 0)
				{
					// 实际上targetRotation需要的是一个相反的值,targetRotation是关节空间下的旋转值
					// 最后关节节点的旋转值(F)等于关节节点的初始旋转值(S) * targetRotation的逆(T^t)， ^t是取逆的意思
					// 即F = S * T^t
					// 两边同时取逆 =》 F^t = T * S^t
					// 两边同乘S =》 T = F^t * S
					// 其中F^t则是Quaternion.Inverse(masterRigidTransforms[i].localRotation * localToJointSpace[i])， S是startLocalRotation[i]，则可以算出T：targetRotation
					configurableJoints[i].targetRotation = Quaternion.Inverse(masterRigidTransforms[i].localRotation * localToJointSpace[i]) * startLocalRotation[i];
					//configurableJoints[i].targetRotation = Quaternion.Inverse(localToJointSpace[i]) * Quaternion.Inverse(masterRigidTransforms[i].localRotation) * startLocalRotation[i];
				}
			}
			frameCounter++;
		}

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		/// <summary>
		/// 设置关节的扭矩和旋转阻尼
		/// </summary>
		/// <param name="positionSpring">扭矩（弹性）</param>
		/// <param name="positionDamper">阻尼</param>
		public void SetJointTorque (float positionSpring, float positionDamper = -1)
		{
			if(positionDamper == -1)
            {
				positionDamper = jointDamping;
			}
			for (int i = 1; i < configurableJoints.Length; i++) // Do for all configurable joints
			{
				jointDrive.positionSpring = positionSpring * maxJointTorqueProfile[i];		// 扭矩，数值越大、旋转弹性越大，则越快旋转到目标旋转值
				jointDrive.positionDamper = positionDamper * jointDampingProfile[i];		// 阻尼，数值越大，旋转阻力越大
				configurableJoints[i].slerpDrive = jointDrive;
			}
			maxJointTorque = positionSpring;
			jointDamping = positionDamper;
		}
		
		/// <summary>
		/// 设置关节位移限制
		/// </summary>
		/// <param name="jointLimits"></param>
		public void EnableJointLimits (bool jointLimits)
		{
			for (int i = 1; i < configurableJoints.Length; i++) // Do for all configurable joints
			{
				if (jointLimits)
				{
					configurableJoints[i].angularXMotion = ConfigurableJointMotion.Limited;
					configurableJoints[i].angularYMotion = ConfigurableJointMotion.Limited;
					configurableJoints[i].angularZMotion = ConfigurableJointMotion.Limited;
				}
				else
				{
					configurableJoints[i].angularXMotion = ConfigurableJointMotion.Free;
					configurableJoints[i].angularYMotion = ConfigurableJointMotion.Free;
					configurableJoints[i].angularZMotion = ConfigurableJointMotion.Free;
				}
			}
		}

		/// <summary>
		/// 不允许角度超过180
		/// </summary>
		/// <param name="angle"></param>
		/// <returns></returns>
		private float FixEuler (float angle) // For the angle in angleAxis, to make the error a scalar
		{
			if (angle > 180f)
				return angle - 360f;
			else
				return angle;
		}

		/// <summary>
		/// 用于位置的混合
		/// 详解：https://blog.csdn.net/QQKeith/article/details/106449496
		/// </summary>
		/// <param name="P">P项常数</param>
		/// <param name="D">D项常数</param>
		/// <param name="signal">结果</param>
		/// <param name="error">与常规位置的误差</param>
		/// <param name="lastError">误差的变化率</param>
		/// <param name="reciDeltaTime">Time.fixedDeltaTime的倒数</param>
		public static void PDControl (float P, float D, out Vector3 signal, Vector3 error, ref Vector3 lastError, float reciDeltaTime) // 物理PD控制器
		{
			// theSignal = P * (theError + D * theDerivative) This is the implemented algorithm.
			signal = P * (error + D * ( error - lastError ) * reciDeltaTime);
			lastError = error;
		}
	}
}