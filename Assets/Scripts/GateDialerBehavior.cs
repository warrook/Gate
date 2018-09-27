using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GateDialerBehavior : MonoBehaviour
{
	public GameObject ringOuter;
	public GameObject ringInner;
	public int speed;
	public float targetRot;
	public float targetRot2;
	public bool reset = false;
	private Quaternion outerRotation;
	private Quaternion innerRotation;

	bool RingRotation(GameObject ring, float target, int direction)
	{
		if (ring.transform.localRotation != Quaternion.Euler(0f, 0f, target))
		{
			float step = (speed * direction) * Time.deltaTime;
			float delta = Mathf.Abs(Mathf.DeltaAngle(ring.transform.localRotation.eulerAngles.z, target));

			ring.transform.Rotate(0f, 0f, step);
			//Debug.Log("Step: " + delta.ToString() + " <= " + step.ToString());
			if (delta <= Mathf.Abs(step))
			{
				ring.transform.localRotation = Quaternion.Euler(0f, 0f, target);
				return true;
			}
			return false;
		}
		else return true;
	}

	// Use this for initialization
	void Start ()
	{
		//outerRotation = Quaternion.Euler(0f, 0f, targetRot);
		//innerRotation = Quaternion.Euler(0f, 0f, targetRot2);
	}
	
	// Update is called once per frame
	void Update ()
	{
		if (reset)
		{
			RingRotation(ringOuter, 0f, 1);
			RingRotation(ringInner, 0f, -1);
		}
		else if (RingRotation(ringOuter, targetRot, -1) == false)
			RingRotation(ringInner, targetRot, -1);
		else if (RingRotation(ringInner, targetRot2, 1))
		{
			reset = true;
		}

	}
}
