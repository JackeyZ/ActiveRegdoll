//#define EXTRATUNING
#define SIMPLEFOOTIK
using UnityEngine;
using System.Collections;

namespace AnimFollow
{
	public class RagdollControl_AF : MonoBehaviour
	{
		// Add this script to the ragdoll
		
		public readonly int version = 7; // The version of this script

		// This kind of a state machine that takes the character through the states: colliding, falling, matching the masters pose and getting back up
		// 这是一种状态机，它会让角色经历各种状态:碰撞，坠落，匹配主姿态，并快速返回动画组件

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		AnimFollow_AF animFollow;					// 控制布娃娃肌肉的脚本，与本脚本一样，添加到布娃娃的根节点
		PlayerMovement_AF playerMovement;           // 告诉角色控制器当我们在碰撞后停止时不要移动。
		Animator animator;							// Reference to the animator component.
		HashIDs_AF hash;							// Reference to the HashIDs.
#if SIMPLEFOOTIK
		SimpleFootIK_AF simpleFootIK;
#endif

		public Transform ragdollRootBone;		// 布娃娃骨骼的根节点， A transform representative of the ragdoll position and rotation. ASSIGN IN INSPECTOR or it will be auto assigned to the first transform with a rigid body
		GameObject master;                      // 最初由动画控制的主角色. 自动分配
		Rigidbody[] slaveRigidBodies;           // 包含布娃娃中的所有刚体. 只用于Limb脚本
		Transform masterRootBone;               // 主动画游戏对象骨骼根节点. 自动分配
		public Transform[] IceOnGetup;          // 这里实际存的是左大腿、左小腿、右大腿、右小腿 Theese rigidbodies will get slipery during getup to avoid snagging（这些刚体在穿戴过程中会变得很滑，以避免被绊住）
		public string[] ignoreCollidersWithTag = {"IgnoreMe"}; // 带有这些标签的碰撞器不会影响布娃娃的强度

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		// These ranges are not at all holy. Feel free to extend the ranges
		[Range(10f, 170f)] public float getupAngularDrag = 50f;		// Custom drag values during getup animations
		[Range(10f, 50f)] public float fallAngularDrag = 20f;		// Custom drag values during fall
		[Range(5f, 85f)] public float getupDrag = 25f;				// Custom drag values during getup animations

		[Range(.5f, 4.5f)] public float fallLerp = 1.5f;            // 决定了碰撞后角色失去控制的速度
		[Range(0f, .2f)] public float residualTorque = 0f;          // 碰撞后立即产生的扭矩
		[Range(0f, .2f)] public float residualForce = .1f;          // 碰撞后立即产生的力
		[Range(0f, 120f)] public float residualJointTorque = 120f;	// 关节扭矩
		[Range(0f, 1f)] public float residualIdleFactor = 0f;       // Allows for lower residual strength if hit when in idle animation（当播放idle动画的时候被击中， 允许较低的残余强度）

		[Range(2f, 26f)] public float graceSpeed = 8f;			// The relative speed limit for a collision to make the character dose off（相对速度阈值）
		[Range(.1f, 1.7f)] public float noGhostLimit = .5f;		// The Limit of limbError that is allowed before the character doses off, given certain conditions
		[Range(5f, 45f)] public float noGhostLimit2 = 15f;      // The Limit of limbError that is allowed before the character doses off, under all circumastances. This prevents you from going through walls like a ghost :)
																// 在任何情况下，角色在射击结束前所允许的动作极限。这可以防止你像鬼一样穿过墙壁
		[Range(0f, 1.2f)] public float glideFree = .3f;         // 如果碰撞不严重，使角色从物体表面掠过

		// These are shown in the inspector for you to get a feel for the states
		public bool falling = false;			// 是否在下落状态
		public bool gettingUp = false;			// 是否在起身状态
		public bool jointLimits = false;		// 关节限制

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		Vector3 limbError;                      // 从AnimFollow读取。包含肢体的总位置误差

		// The parmeters below are parameters you might want to tune but they are not critical. Make them public if you want to mess with them. Uncomment first line of script. Changes not persistent if you undefine EXTRATUNING.
#if EXTRATUNING
		public bool fellOnSpeed = false;				// For tuning. Tells the reason the fall was triggered

		public float limbErrorMagnitude;				// This may be interesting to see if you are tuning the noGhostLimits. Read from AnimFollow. Contains the magnitude of the total position error of the limbs. When this is above the noGhostLimit falling is triggered

		[Range(0f, .4f)] public float settledSpeed = .2f;				// When ragdollRootBoone goes below this speed the falling state is through and the get up starts
		[Range(0f, .6f)] public float masterFallAnimatorSpeedFactor = .4f;	// Animator speed during transition to get up animations
		[Range(0f, .4f)] public float getup1AnimatorSpeedFactor = .25f; 	// Animation speed during the initial part of the get up state is getup1AnimatorSpeedFactor * animatorSpeed
		[Range(0f, 1f)] public float getup2AnimatorSpeedFactor = .65f; 	// Animation speed during the later part of the get up state is getup1AnimatorSpeedFactor * animatorSpeed

		[Range(0f, 10f)] public float contactTorque = 1f;				// The torque when in contact with other colliders
		[Range(0f, 10f)] public float contactForce = 2f;
		[Range(0f, 50000f)] public float contactJointTorque = 1000f;

		[Range(.04f, .48f)] public float getupLerp1 = .15f;		// Determines the initial regaining of strength after the character fallen to ease the ragdoll to the masters pose
		[Range(.5f, 6.5f)] public float getupLerp2 = 2f;			// Determines the regaining of strength during the later part of the get up state
		[Range(.05f, .65f)] public float wakeUpStrength = .2f;		// A number that defines the degree of strength the ragdoll must reach before it is assumed to match the master pose and start the later part of the get up state

		[Range(0f, 700f)] public float toContactLerp = 70f;				// Determines how fast the character loses strength when in contact
		[Range(0f, 10f)] public float fromContactLerp = 1f;				// Determines how fast the character gains strength after freed from contact
		
		[Range(0f, 100f)] public float maxTorque = 100f;				// The torque when not in contact with other colliders
		[Range(0f, 100f)] public float maxForce = 100f;
		[Range(0f, 10000f)] public float maxJointTorque = 10000f;

		[Range(0f, 1f)] public float maxErrorWhenMatching = .1f;		// The limit of error acceptable to consider the ragdoll to be matching the master. Is condition for going to normal operation after getting up
#else
		bool fellOnSpeed = false;					// 调节参数。说明了触发坠落的原因

		float limbErrorMagnitude;					// This may be interesting to see if you are tuning the noGhostLimits. Read from AnimFollow. Contains the magnitude of the total position error of the limbs. When this is above the noGhostLimit falling is triggered
													// 如果您正在调优noGhostLimits，这可能会很有趣。从AnimFollow读取。包含了肢体的总位置误差的大小。当这高于noGhostLimit时触发下降
		float settledSpeed = .1f;                   // 当ragdollRootBoone低于这个速度时，下降状态就结束了，进入起身状态
		float masterFallAnimatorSpeedFactor = .4f;	// Animator speed during transition to get up animations
													// 动画转换时的速度
		float getup1AnimatorSpeedFactor = .35f;     // Animation speed during the initial part of the get up state is getup1AnimatorSpeedFactor * animatorSpeed
													// 启动状态初始阶段的动画速度为getup1AnimatorSpeedFactor * animatorSpeed
		float getup2AnimatorSpeedFactor = .85f;     // Animation speed during the later part of the get up state is getup1AnimatorSpeedFactor * animatorSpeed
													// 启动状态后期的动画速度
		
		float getupLerp1 = .15f;					// 决定角色倒下后最初的力量恢复，以缓和布娃娃到主动画的姿势
		float getupLerp2 = 2f;						// Determines the regaining of strength during the later part of the get up state（决定了起身状态的后半部分重新获得的力量）
		float wakeUpStrength = .2f;                 // A number that defines the degree of strength the ragdoll must reach before it is assumed to match the master pose and start the later part of the get up state

		float toContactLerp = 70f;					// 碰撞时失去力量的速度
		float fromContactLerp = 1f;                 // 脱离碰撞后恢复最大扭矩的速度

		float contactTorque = 1f;                   // 碰撞时候的扭矩（数值很小，表示节点跟随主动画的力量很小，相当于布娃娃占据的权重很大）
		float contactForce = 2f;                    // 碰撞时力量
		float contactJointTorque = 1000f;           // 关节碰撞时候的扭矩

		float maxTorque = 100f;                     // 非碰撞时候的扭矩
		float maxForce = 100f;                      // 非碰撞时力量
		float maxJointTorque = 10000f;				// 关节非碰撞时候的扭矩

		float maxErrorWhenMatching = .1f;			// The limit of error acceptable to consider the ragdoll to be matching the master. Is condition for going to normal operation after getting up
													// 可接受的误差极限，以考虑布娃娃与主动画匹配。起身后是否有正常操作的条件
#endif
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		[HideInInspector]
		public float orientateY = 0f;           // The world y-coordinate the master transform will be at after a fall. If you move your character vertically you want to set this to match
												// 世界的y坐标，将是在主动画跌倒之后。如果你垂直移动你的角色，你需要设置这个去匹配

		[HideInInspector] public float collisionSpeed;      // 记录碰撞体与布娃娃刚体接触时候的相对速度（Limb_AF组件会记录该值）
		[HideInInspector] public int numberOfCollisions;    // 当前与布娃娃接触的碰撞体数量（Limb_AF组件会记录接触的数目）
		float animatorSpeed;					// 从PlayerMovement脚本读取
		int secondaryUpdateSet;                 // 从AnimFollow脚本读取
		float[] noIceDynFriction;               // 用于储存IceOnGetup里Collider组件的PhysicMaterial的动摩檫力
		float[] noIceStatFriction;              // 用于储存IceOnGetup里Collider组件的PhysicMaterial的静摩檫力
		float drag;                             // 从AnimFollow脚本读取
		float angularDrag;

		// 这些参数不是用来调优的
		float contactTime;                      // 碰撞时长，布娃娃上次碰撞开始发生在多久之前（碰撞结束会清零）
		float noContactTime = 10f;              // 未发生碰撞时长，布娃娃上次碰撞结束发生在多久之前
		Quaternion rootboneToForward;			// masterRootBone 相对于 master.transform.forward 的旋转
		[HideInInspector] public bool shotByBullet = false;			// 当前帧是否被子弹打中
		bool userNeedsToAssignStuff = false;	// 用于记录必要的设置是否准备好（false表示准备好了）
		bool delayedGetupDone = false;          // 用于延迟设置gettingUp为假， Used to delay setting gettingUp to false if still in contakt after get up state
		bool localTorqUserSetting;				// Saves the user setting from AnimFollow
		bool orientate = false;                 // starts the process of matching the ragdoll to the master（开始将布娃娃与主动画角色配对的过程）
		bool orientated = true;                 // starts the process of matching the ragdoll to the master（开始将布娃娃与主动画角色配对的过程）
		bool getupState = false;				// 废弃
		bool isInTransitionToGetup = false;
		bool wasInTransitionToGetup = false;
		public bool stayDeadOnHeadShot = false; // 如果设置true, 击中头部后角色将保持死亡，将在主摄像机不可见的时候被摧毁
		[HideInInspector] public bool shotInHead = false;			// 当前帧是否被子弹打中头部
		ulong frameCount;

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		void Awake () // Initialize
		{
			if (!WeHaveAllTheStuff()) // Check
			{
				userNeedsToAssignStuff = true;
				return;
			}

			animatorSpeed = playerMovement.animatorSpeed; // 从playerMovement读取动画速度
			animator.speed = animatorSpeed; // set the animator speed to the setting in player movement. RagdollControl varies the animator speed, best to not set animator speed anywhere else
			secondaryUpdateSet = animFollow.secondaryUpdate;

			// // 把本脚本的力矩信息设置到animFollow脚本里
			animFollow.maxTorque = maxTorque; 
			animFollow.maxForce = maxForce;
			animFollow.maxJointTorque = maxJointTorque;

			// 给所有刚体添加Limb_AF组件
			slaveRigidBodies = GetComponentsInChildren<Rigidbody>(); // 获取布娃娃骨骼里的所有刚体
			foreach (Rigidbody slaveRigidBody in slaveRigidBodies)
            {
				// 分配一个Limb_AF到所有刚体。如果任何一个骨骼刚体与另一个碰撞器接触，该脚本将报告给ragdollcontrol
				slaveRigidBody.gameObject.AddComponent<Limb_AF>();
			}

			// 遍历IceOnGetup，把里面的静摩擦力和动摩擦里存起来
			System.Array.Resize(ref noIceDynFriction, IceOnGetup.Length);
			System.Array.Resize(ref noIceStatFriction, IceOnGetup.Length);
			for (int m = 0; m < IceOnGetup.Length; m++)
			{
				noIceDynFriction[m] = IceOnGetup[m].GetComponent<Collider>().material.dynamicFriction;
				noIceStatFriction[m] = IceOnGetup[m].GetComponent<Collider>().material.staticFriction;
			}

			// 从animFollow获取布娃娃的刚体骨骼里应用的阻力和旋转阻力
			drag = animFollow.drag;
			angularDrag = animFollow.angularDrag;

			// 脚本版本控制，检查各个脚本的版本是否相同
			if (ragdollRootBone.GetComponent<Limb_AF>().version != version)
				Debug.LogWarning("RagdollControll script is version " + version + " but Limb script is version " + ragdollRootBone.GetComponent<Limb_AF>().version + "\n");
			if (animFollow.version != version)
				Debug.LogWarning("RagdollControll script is version " + version + " but animFollow script is version " + animFollow.version + "\n");
			if (playerMovement.version != version)
				Debug.LogWarning("RagdollControll script is version " + version + " but playerMovement script is version " + playerMovement.version + "\n");
			if (playerMovement.GetComponent<HashIDs_AF>().version != version)
				Debug.LogWarning("RagdollControll script is version " + version + " but HashIDs script is version " + playerMovement.GetComponent<HashIDs_AF>().version + "\n");
		}

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		/// <summary>
		/// 通过该方法，控制布娃娃刚体的各种参数和动画状态机的状态，每帧调用
		/// </summary>
		public void DoRagdollControl() // 需要与AnimFollow同步
		{
			if (userNeedsToAssignStuff)
				return;

			// 如果勾选了爆头保持死亡并且被射中头部，则保持死亡姿势
			if (stayDeadOnHeadShot && shotInHead)
			{
				animFollow.maxTorque = 0f;								// 最大扭矩设置为0，表示全部由布娃娃控制
				animFollow.maxForce = 0f;
				animFollow.maxJointTorque = 0f;
				animFollow.SetJointTorque (animFollow.maxJointTorque);	// 立即设置关节的扭矩，不要等animfollow.secondaryUpdate
				animFollow.angularDrag = angularDrag;
				animFollow.drag = drag;
				simpleFootIK.userNeedsToFixStuff = true;				// Just disabling footIK
				playerMovement.inhibitMove = true;						// 限制角色移动

				// 如果不可见，则销毁游戏对象
				Renderer ragdollRenderer;
				if ((ragdollRenderer = transform.GetComponentInChildren<Renderer>()) && !ragdollRenderer.isVisible)
					Destroy(this.transform.root.gameObject);

				return;
			}

			// 第二帧做一个初始化
			if (frameCount == 2) // 应该在Awake中完成，但是mecanim对一些模型做了一个奇怪的初始旋转
			{
				// 世界空间下主动画骨骼根节点的旋转值（masterRootBone.rotation）取逆之后xyz值都取反，相当于假设原本绕x轴转30°，取逆之后变成绕x轴转-30°
				// 算出来的是master在masterRootBone空间下的旋转值，masterRootBone.rotation是基底的旋转值，这里取逆之后再乘，就相当于把master世界空间下的旋转值转换到rootBone空间下
				rootboneToForward = Quaternion.Inverse(masterRootBone.rotation) * master.transform.rotation;
			}
			frameCount++;
	//		Debug.DrawRay(ragdollRootBone.position, ragdollRootBone.rotation * rootboneToForward * Vector3.forward); // Open this and check that the ray is pointing as the nose of the charater

			// 是否处于起身状态
			getupState = animator.GetCurrentAnimatorStateInfo(0).fullPathHash.Equals(hash.getupFront) || animator.GetCurrentAnimatorStateInfo(0).fullPathHash.Equals(hash.getupBack) || animator.GetCurrentAnimatorStateInfo(0).fullPathHash.Equals(hash.getupFrontMirror) || animator.GetCurrentAnimatorStateInfo(0).fullPathHash.Equals(hash.getupBackMirror);
			// 记录上一帧是否正过渡到起身状态
			wasInTransitionToGetup = isInTransitionToGetup;
			// 是否正过渡到起身状态
			isInTransitionToGetup = animator.GetAnimatorTransitionInfo(0).fullPathHash.Equals(hash.anyStateToGetupFront) || animator.GetAnimatorTransitionInfo(0).fullPathHash.Equals(hash.anyStateToGetupBack) || animator.GetAnimatorTransitionInfo(0).fullPathHash.Equals(hash.anyStateToGetupFrontMirror) || animator.GetAnimatorTransitionInfo(0).fullPathHash.Equals(hash.anyStateToGetupBackMirror);

			limbError = animFollow.totalForceError;		// 获取布娃娃偏差
			limbErrorMagnitude = limbError.magnitude;   // 取模，算出偏差长度

			////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

			// 下面的代码首先检查我们是否被足够的力量击中，然后做:
			// 禁止PlayerMovement脚本中的移动，跌落，定向主动画角色。方便布娃娃控制主动画角色，播放起身动画，再做一次全力量的移动
			// inhibit movements in PlayerMovement script, falling, orientate master. ease ragdoll to master pose, play getup animation, go to full strength and anable movements again. 

			// 如果:我们以足够高的速度撞击，或者如果角色的变形过大
			if (shotByBullet || numberOfCollisions > 0 && (collisionSpeed > graceSpeed || (!(gettingUp || falling) && limbErrorMagnitude > noGhostLimit)) || (limbErrorMagnitude > noGhostLimit2 && orientated))
			{
				// 如果当前帧不是跌落状态
				if (!falling)
				{
					// 撞击后的初始强度
					if (!animator.GetCurrentAnimatorStateInfo(0).fullPathHash.Equals(hash.idle) && ! getupState) // 不在idle状态
					{
						animFollow.maxTorque = residualTorque;		// 撞击后第一帧跟随扭矩设置为0，表明完全不跟随主动画角色的节点，表示节点完全由布娃娃控制
						animFollow.maxForce = residualForce;
						animFollow.maxJointTorque = residualJointTorque;
						animFollow.SetJointTorque (residualJointTorque); // Do not wait for animfollow.secondaryUpdate
					}
					else // If was in Idle state
					{
						Debug.Log("was in idle state" + residualIdleFactor);
						animFollow.maxTorque = residualTorque * residualIdleFactor;
						animFollow.maxForce = residualForce * residualIdleFactor;
						animFollow.maxJointTorque = residualJointTorque * residualIdleFactor;
						animFollow.SetJointTorque (animFollow.maxJointTorque); // Do not wait for animfollow.secondaryUpdate
					}

					animFollow.EnableJointLimits(true);
					jointLimits = true;
					animFollow.secondaryUpdate = 100;		// 开始跌落的第一帧把非刚体融合的参数改大，这样与主动画节点融合的速度会变慢
					// 恢复刚体的摩檫力
					for (int m = 0; m < IceOnGetup.Length; m++) // turn off iceOnGetup
					{
						IceOnGetup[m].GetComponent<Collider>().material.dynamicFriction = noIceDynFriction[m];
						IceOnGetup[m].GetComponent<Collider>().material.staticFriction = noIceStatFriction[m];
					}

					animFollow.angularDrag = fallAngularDrag;		// 设置角阻力
					animFollow.drag = drag;							// 设置阻力
				}

				shotByBullet = false;			// 恢复默认值
				falling = true;					// 设置当前状态是正在下落
				gettingUp = false;				// 设置当前状态不是起身状态
				orientated = false;             // 面向
				animator.speed = animatorSpeed;	// 设置动画状态机的动画播放速度
				delayedGetupDone = false;

				// 是否发生碰撞，并且碰撞速度大于阈值
				fellOnSpeed = numberOfCollisions > 0 && collisionSpeed > graceSpeed; // For tuning. If you want to know if the fall was triggered by the speed of the collision
			}
			// 跌落中或起身中
			else if (falling || gettingUp) // Code do not run in normal operation
			{	
				// 起身中
				if (gettingUp)
				{
					// Wait until transition to getUp is done so that the master animation is lying down before orientating the master to the ragdoll rotation and position
					// 等待直到过渡到起身动画完成，以便主动画对象躺下，然后将主动画定向到布娃娃旋转和位置
					// 检查是否需要定向(选择哪种起身动画，正面还是背面)并且即刚刚过渡到起身动画（当前帧不是过渡到起身的动画，并且上一帧是过渡到起身的动画）
					if (orientate && !isInTransitionToGetup && wasInTransitionToGetup)
					{
						falling = false;	// 标记不是跌落状态

						// Here the master gets reorientated to the ragdoll which could have ended its fall in any direction and position
						// 在这里，主动画对象被重新定位到布娃娃上，布娃娃可以在任何方向和位置结束下落
						master.transform.rotation = ragdollRootBone.rotation * Quaternion.Inverse(masterRootBone.rotation) * master.transform.rotation;
						master.transform.rotation = Quaternion.LookRotation(new Vector3(master.transform.forward.x, 0f, master.transform.forward.z), Vector3.up); 
						master.transform.Translate(ragdollRootBone.position - masterRootBone.position, Space.World);
#if SIMPLEFOOTIK
						simpleFootIK.extraYLerp = .02f;
						simpleFootIK.leftFootPosition = ragdollRootBone.position + Vector3.up;
						simpleFootIK.rightFootPosition = ragdollRootBone.position + Vector3.up;
#else
						master.transform.position = new Vector3(master.transform.position.x, orientateY, master.transform.position.z);
#endif
						orientate = false;  // 标记不需要定向起身动画
						orientated = true;  // 标记起身动画定向已完成

						// 把刚体摩檫力设为0
						for (int m = 0; m < IceOnGetup.Length; m++) // Turn On iceOnGetup
						{
							IceOnGetup[m].GetComponent<Collider>().material.dynamicFriction = 0f;
							IceOnGetup[m].GetComponent<Collider>().material.staticFriction = 0f;
						}
						animFollow.angularDrag = getupAngularDrag;				// 设置角阻力
						animFollow.drag = getupDrag;							// 设置阻力
					}

					// 检查是否已经定向完毕
					if (orientated)
					{
						if (animFollow.maxTorque < wakeUpStrength) // Ease the ragdoll to the master pose. WakeUpStrength limit should be set so that the radoll just has reached the master pose
						{
							master.transform.Translate((ragdollRootBone.position - masterRootBone.position) * .5f, Space.World);

							animator.speed = getup1AnimatorSpeedFactor * animatorSpeed; // Slow the animation briefly to give the ragdoll time to ease to master pose
							animFollow.maxTorque = Mathf.Lerp(animFollow.maxTorque, contactTorque, getupLerp1 * Time.fixedDeltaTime); // We now start lerping the strength back to the ragdoll. Do until strength is wakeUpStrength. Animation is running wery slowly
							animFollow.maxForce = Mathf.Lerp(animFollow.maxForce, contactForce, getupLerp1 * Time.fixedDeltaTime);
							animFollow.maxJointTorque = Mathf.Lerp(animFollow.maxJointTorque, contactJointTorque, getupLerp1 * Time.fixedDeltaTime);
							animFollow.secondaryUpdate = 20;
						}
						else if (!(isInTransitionToGetup || getupState)) // Getting up is done. We are back in Idle (if not delayed)
						{
							playerMovement.inhibitMove = false; // Master is able to move again
#if SIMPLEFOOTIK
							simpleFootIK.extraYLerp = 1f;
#endif
							animFollow.angularDrag = angularDrag;
							animFollow.drag = drag;
							animator.speed = animatorSpeed;
							animFollow.secondaryUpdate = secondaryUpdateSet;

							for (int m = 0; m < IceOnGetup.Length; m++) // turn of iceOnGetup
							{
								IceOnGetup[m].GetComponent<Collider>().material.dynamicFriction = noIceDynFriction[m];
								IceOnGetup[m].GetComponent<Collider>().material.staticFriction = noIceStatFriction[m];
							}

							if (limbErrorMagnitude < maxErrorWhenMatching) // Do not go to full strength unless ragdoll is matching master (delay)
							{
								gettingUp = false; // Getting up is done
								delayedGetupDone = false;
								playerMovement.inhibitRun = false;
							}
							else
							{
								delayedGetupDone = true;
								playerMovement.inhibitRun = true; // Inhibit running until ragdoll is matching master again
							}
						}
						else // Lerp the ragdoll to contact strength during get up
						{
							animator.speed = getup2AnimatorSpeedFactor * animatorSpeed; // Animation speed during get up state
							animFollow.maxTorque = Mathf.Lerp(animFollow.maxTorque, contactTorque, getupLerp2 * Time.fixedDeltaTime);
							animFollow.maxForce = Mathf.Lerp(animFollow.maxForce, contactForce, getupLerp2 * Time.fixedDeltaTime);
							animFollow.maxJointTorque = Mathf.Lerp(animFollow.maxJointTorque, contactJointTorque, getupLerp2 * Time.fixedDeltaTime);
							animFollow.secondaryUpdate = secondaryUpdateSet * 2;
							if (jointLimits)
							{
								animFollow.EnableJointLimits(false);
								jointLimits = false;
							}
						}
					}
				}
				// 跌落中
				else
				{
					// Lerp force to zero from residual values
					animFollow.maxTorque = Mathf.Lerp(animFollow.maxTorque, 0f, fallLerp * Time.fixedDeltaTime);
					animFollow.maxForce = Mathf.Lerp(animFollow.maxForce, 0f, fallLerp * Time.fixedDeltaTime);
					animFollow.maxJointTorque = Mathf.Lerp(animFollow.maxJointTorque, 0f, fallLerp * Time.fixedDeltaTime);
					animFollow.SetJointTorque (animFollow.maxJointTorque); // Do not wait for animfollow.secondaryUpdate

					// Orientate master to ragdoll and start transition to getUp when settled on the ground. Falling is over, getting up commences
					// 判断刚体的速度是否低于阈值，如果低于，则可以切换成起身状态
					if (ragdollRootBone.GetComponent<Rigidbody>().velocity.magnitude < settledSpeed) // && contactTime + noContactTime > .4f)
					{
						gettingUp = true;														// 设置成起身状态
						orientate = true;														// 表示开始与主动画角色的起身动画进行匹配
						playerMovement.inhibitMove = true;										// 抑制角色移动
						animator.speed = masterFallAnimatorSpeedFactor * animatorSpeed;			// 动画速度过渡到起身状态
						animFollow.maxTorque = 0f;												// 跟随主动画的扭矩设置为0，表示不跟随主动画，避免在定向阶段抽搐
						animFollow.maxForce = 0f;
						animFollow.maxJointTorque = 0f;
						animator.SetFloat(hash.speedFloat, 0f, 0f, Time.fixedDeltaTime);		// 起身状态把移动速度设置为0，避免移动

						// rootboneToForeward是 master 相对于 masterRootBone 的旋转,即master在masterRootBone空间下的旋转值
						// 定理1：transform.rotation * Vector.forward 相当于 transform.forward
						// 第一步：rootboneToForward * Vector3.forward, master的z轴在masterRootBone空间下方向，这里的模型算出来是Vector3.up
						// 第二步：ragdollRootBone.rotation * 第一步得出的向量(Vector3.up)，就是算出ragdollRootBone中y轴向量的旋转值，相当于ragdollRootBone.up
						// 计算出根骨骼朝向
						Vector3 rootBoneForward = ragdollRootBone.rotation * rootboneToForward * Vector3.forward;

						// 判断布娃娃是否是俯着身体
						if (Vector3.Dot(rootBoneForward, Vector3.down) >= 0f) // Check if ragdoll is lying on its back or front, then transition to getup animation
						{
							if (!animator.GetCurrentAnimatorStateInfo(0).fullPathHash.Equals(hash.getupFront))
								animator.SetBool(hash.frontTrigger, true);
							else // if (!anim.GetCurrentAnimatorStateInfo(0).IsName("GetupFrontMirror"))
								animator.SetBool(hash.frontMirrorTrigger, true);
						}
						// 仰着身体
						else
						{
							if (!animator.GetCurrentAnimatorStateInfo(0).fullPathHash.Equals(hash.getupBack))
								animator.SetBool(hash.backTrigger, true);
							else // if (!anim.GetCurrentAnimatorStateInfo(0).IsName("GetupFrontMirror"))
								animator.SetBool(hash.backMirrorTrigger, true);
						}
					}
				}
			}// 跌落中或起身中end

			collisionSpeed = 0f; // 重置成0

			////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

			// 下面的代码也在正常操作中运行(不跌倒或站起来)

			// 检查是否没有发生碰撞
			if (numberOfCollisions == 0) // Not in contact
			{
				noContactTime += Time.fixedDeltaTime;
				contactTime = 0f;

				// When not in contact character has maxStrenth strength
				// 当没有接触角色的时候有最大的力量
				if (!(gettingUp || falling) || delayedGetupDone)
				{
					// 非碰撞时随时间逐步增加扭矩
					animFollow.maxTorque = Mathf.Lerp(animFollow.maxTorque, maxTorque, fromContactLerp * Time.fixedDeltaTime);
					animFollow.maxForce = Mathf.Lerp(animFollow.maxForce, maxForce, fromContactLerp * Time.fixedDeltaTime);
					animFollow.maxJointTorque = Mathf.Lerp(animFollow.maxJointTorque, maxJointTorque, fromContactLerp * Time.fixedDeltaTime);
				}
			}
			// 发生碰撞
			else // In contact
			{
				contactTime += Time.fixedDeltaTime;
				noContactTime = 0f;

				// When in contact character has only contact strength
				// 在碰撞时角色只有碰撞的力量
				if (!(gettingUp || falling) || delayedGetupDone)
				{
					// 逐步减少到碰撞时扭矩
					animFollow.maxTorque = Mathf.Lerp(animFollow.maxTorque, contactTorque, toContactLerp * Time.fixedDeltaTime);
					animFollow.maxForce = Mathf.Lerp(animFollow.maxForce, contactForce, toContactLerp * Time.fixedDeltaTime);
					animFollow.maxJointTorque = Mathf.Lerp(animFollow.maxJointTorque, contactJointTorque, toContactLerp * Time.fixedDeltaTime);
				}
			}

			////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

			// 如果布娃娃变形很大，调整玩家的移动，例如，如果我们正在走到一堵墙
			if (noContactTime < .3f && !(gettingUp || falling))
				playerMovement.glideFree = new Vector3(-limbError.x, 0f, -limbError.z) * glideFree;
			else
				playerMovement.glideFree = Vector3.zero;
		}

		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
		////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

		/// <summary>
		/// 判断是否所有设置都符合了
		/// </summary>
		/// <returns></returns>
		bool WeHaveAllTheStuff()
		{
			if (!(animFollow = GetComponent<AnimFollow_AF>()))
			{
				Debug.LogWarning("Missing Script: AnimFollow on " + this.name + "\n");
				return(false);
			}
			// 检查animFollow里面是否有主动画的游戏对象（拖上去的），如果有则存到本脚本的master里面
			else if (!(master = animFollow.master))
			{
				Debug.LogWarning("master not assigned in AnimFollow script on " + this.name + "\n");
				return(false);
			}
#if SIMPLEFOOTIK
			// 主动画对象上是否有SimpleFootIK_AF组件
			else if (!(simpleFootIK = master.GetComponent<SimpleFootIK_AF>()))
			{
				UnityEngine.Debug.LogWarning("Missing script SimpleFootIK script on " + master.name + ".\nAdd it or comment out the directive from top line in the AnimFollow script." + "\n");
				return false;
			}
#else
			else if (master.GetComponent<SimpleFootIK_AF>())
			{
				UnityEngine.Debug.LogWarning("There is a SimpleFootIK script on\n" + master.name + " But the directive in the AnimFollow script is commented out" + "\n");
				return false;
			}
#endif
			// 检查主动画对象是否在场景里激活
			else if (!master.activeInHierarchy)
			{
				Debug.LogWarning("Master of " + this.name + " is not active" + "\n");
				return false;
			}
			else
			{
				if (!ragdollRootBone)
				{
					// 布娃娃骨骼的根节点
					ragdollRootBone = GetComponentInChildren<Rigidbody>().transform;
					//				Debug.Log("ragdollRootBone not assigned in RagdollControll script on " + this.name + ".\nAuto assigning to " + ragdollRootBone.name + "\nThis is probably correct if this is a standard biped." + "\n");
				}
				// 检查布娃娃骨骼的根节点是否有rigidbody组件和根节点是否与本脚本所在游戏对象的根节点一致
				else if (!ragdollRootBone.GetComponent<Rigidbody>() || !(ragdollRootBone.root == this.transform.root))
				{
					ragdollRootBone = GetComponentInChildren<Rigidbody>().transform;
					Debug.LogWarning("ragdollRootBone in RagdollControll script on " + this.name + " has no rigid body component or is not child of ragdoll.\nAuto assigning to " + ragdollRootBone.name + "\nAuto assignment is probably correct if this is a standard biped." + "\n");
				}
				// 找到主动画游戏对象下的骨骼根节点
				int i = 0;
				Transform[] transforms = GetComponentsInChildren<Transform>();
				foreach(Transform transformen in transforms)  // Find the masterRootBoone
				{
					if (transformen == ragdollRootBone)
					{
						masterRootBone = master.GetComponentsInChildren<Transform>()[i];
						break;
					}
					i++;
				}
			}

			// 判断主动画游戏对象是否有PlayerMovement_AF脚本
			if (!(playerMovement = master.GetComponent<PlayerMovement_AF>()))
			{
				Debug.LogWarning("Missing Script: PlayerMovement on " + master.name + "\n");
				return(false);
			}

			// 判断主动画游戏对象是否有animator组件
			if (!(animator = master.GetComponent<Animator>()))
			{
				Debug.LogWarning("Missing Animator on " + master.name + "\n");
				return(false);
			}
			else
			{
				// 检查Animator的culling和update模式
				if (animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
					Debug.Log ("Animator cullingmode on " + this.name + " is not set to always animate.\nIf the masteris hidden the animations will not run." + "\n");
				if (!animator.updateMode.Equals(AnimatorUpdateMode.AnimatePhysics))
					Debug.Log ("Animator on " + this.name + " is not set to animate physics" + "\n");
			}

			// 检查IceOnGetup合法性
			if (IceOnGetup.Length == 0)
			{
				Debug.Log ("Assign left and right calf and thigh to iceOnGetup in script RagdollControl on " + this.name + "\n");
			}
			else if (IceOnGetup[IceOnGetup.Length - 1] == null)
			{
				Debug.LogWarning("Assign left and right calf and thigh to iceOnGetup in script RagdollControl on " + this.name + "\nDo not leave elements as null." + "\n");
				return false;
			}

			// 检查人物根节点是否有ragdollHitByBullet_AF组件
			if (!transform.root.GetComponent<ragdollHitByBullet_AF>())
				Debug.Log("There is no ragdollHitByBullet script on the root transform of " + this.name + "\n");

			if (!(hash = master.GetComponent<HashIDs_AF>()))
			{
				Debug.LogWarning("Missing Script: HashIDs on " + master.name + "\n");
				return(false);
			}

			if (fellOnSpeed)
				print ("This will never show and is here just to avoid a compiler warning");

			return(true);
		}
	}
}
