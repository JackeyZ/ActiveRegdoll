using UnityEngine;
using System.Collections;

namespace AnimFollow
{
	/// <summary>
	/// This script is distributed (automatically by RagdollControl) to all rigidbodies and reports to the RagdollControl script if any limb is currently colliding.
	/// 该脚本被添加(RagdollControl自动添加)到所有刚体，如果任何肢体当前正在发生碰撞,会报告给RagdollControl脚本，
	/// </summary>
	public class Limb_AF : MonoBehaviour
	{
		public readonly int version = 7; // The version of this script

		RagdollControl_AF ragdollControl;
		string[] ignoreCollidersWithTag;
			
		void OnEnable()
		{
			ragdollControl = transform.root.GetComponentInChildren<RagdollControl_AF>();
			ignoreCollidersWithTag  = ragdollControl.ignoreCollidersWithTag;
		}
		
		void OnCollisionEnter(Collision collision)
		{
			bool ignore = false;
			if (!(collision.transform.name == "Terrain") && collision.transform.root != this.transform.root)
			{
				foreach (string ignoreTag in ignoreCollidersWithTag)
				{
					if (collision.transform.tag == ignoreTag)
					{
						ignore = true;
						break;
					}
				}

				if (!ignore)
				{
					ragdollControl.numberOfCollisions++;
					ragdollControl.collisionSpeed = collision.relativeVelocity.magnitude;
//					Debug.Log (collision.transform.name + "\nincreasing");
				}
			}
		}
		
		void OnCollisionExit(Collision collision)
		{
			bool ignore = false;
			if (!(collision.transform.name == "Terrain") && collision.transform.root != this.transform.root)
			{
				foreach (string ignoreTag in ignoreCollidersWithTag)
				{
					if (collision.transform.tag == ignoreTag)
					{
						ignore = true;
						break;
					}
				}

				if (!ignore)
				{
					ragdollControl.numberOfCollisions--;
	//				Debug.Log (collision.transform.name + "\ndecreasing");
				}
			}
		}
	}
}
