
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotaTest : MonoBehaviour
{
    public GameObject target;
    public GameObject target1;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!target)
        {
            return;
        }
        //ConfigurableJoint joint = GetComponent<ConfigurableJoint>(); 
        //Vector3 forward = Vector3.Cross(joint.axis, joint.secondaryAxis);  // axis会ConfigurableJoint组件中覆盖本地x轴方向(原本绕x轴的旋转会变成绕axis轴，这里配合targetRotation实用)、secondaryAxis是y轴，叉乘之后得到z轴方向
        //Vector3 up = joint.secondaryAxis;
        if (!target1)
        {
            return;
        }
        //Quaternion localToJointSpace = Quaternion.LookRotation(forward, up);
        //Debug.Log(forward);
        //Debug.Log(localToJointSpace);
        //transform.rotation = localToJointSpace * target.transform.rotation;
        //transform.rotation = Quaternion.Inverse(target.transform.rotation) * Quaternion.Inverse(target1.transform.rotation);
        //joint.targetRotation = Quaternion.Inverse(target.transform.rotation);// * Quaternion.Inverse(target1.transform.rotation);
        Debug.Log(target.transform.position - target1.transform.position);
        Debug.Log(Quaternion.Inverse(target1.transform.rotation));
        Debug.Log(Quaternion.Inverse(target1.transform.rotation) * (target.transform.position - target1.transform.position));
        //transform.position = Quaternion.Inverse(target1.transform.rotation) * (target.transform.position - target1.transform.position);

    }
}
