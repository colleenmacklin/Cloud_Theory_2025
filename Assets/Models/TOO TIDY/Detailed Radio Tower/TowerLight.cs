using UnityEngine;
using System.Collections;

public class TowerLight : MonoBehaviour {

	public Light beacon;
	public float interval;
	private float timeElapsed;

	
	void Update () {
		timeElapsed += Time.deltaTime;
		if(timeElapsed > interval) {
			if(beacon.enabled == true) {
				beacon.enabled = false;
			} else {
				beacon.enabled = true;
			}
			timeElapsed = 0f;
		}
	}
}
