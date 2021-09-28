using UnityEngine;
using System.Collections;

namespace AnimFollow
{
	/// <summary>
	/// 挂载到主动画角色上
	/// </summary>
	public partial class SimpleFootIK_AF : MonoBehaviour // partial关键字的说明，可以把同一个类放到不同文件下编写的关键字
	{	
		void Awake()
		{
			Awake2();
		}

		void FixedUpdate()
		{
			deltaTime = Time.fixedDeltaTime;
			DoSimpleFootIK();
		}

		void DoSimpleFootIK()
		{	
			if (userNeedsToFixStuff)
			{
				animFollow.DoAnimFollow(); // Only here to make the dead on headshot feature work properly
				return;
			}

			ShootIKRays();

			PositionFeet();

			animFollow.DoAnimFollow();
		}
	}
}
