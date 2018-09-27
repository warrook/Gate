using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class Galaxy
{
	public float Radius { get; set; }
	public float Height { get; set; }
	public List<Gate> Gates { get; set; }
}

public class Gate
{
	public Galaxy Galaxy { get; set; }
	public Vector3 Position { get; set; } // 3D position within the galaxy
	public Vector3 GridPosition { get; set; } // Closest grid point to the actual position
	public int[] StaticAddress { get; set; } // Static address to reduce calculation/variation; will be zero until calculated
	public bool inRange { get; set; } // True if the grid point is close enough to the true position
	public int visited { get; set; } // -1 if unknown; 0 if known; 1 if visited TODO: Make enum

	public override string ToString()
	{
		return Position.ToString();
	}
}

public class GateNetwork : MonoBehaviour
{
	//public List<Gate> gateNetwork = new List<Gate>();
	public List<Galaxy> galaxies = new List<Galaxy>();
	public AnimationCurve distanceCurve;
	public AnimationCurve heightCurve;
	public AnimationCurve rotationCurve;
	private int baseN = 32;
	private int numDigits = 2;
	private float baseRadius = 0.2f; // Radius around point to search for a gate, in fractions of a degree in baseN
	private float true_radius;
	private int maxVal;
	private float nPerDegree;
	private float nPerDist;

	private int easy_gates;

	private void GenerateNetwork()
	{
		List<Gate> gateNetwork = new List<Gate>();

		// Make central cluster
		for (int i = 0; i < 2000; i++)
		{
			Gate g = new Gate() { Galaxy = galaxies[0] };
			Vector3 pos;

			do
			{
				pos = new Vector3(
					Utils.NextGaussian(0f, g.Galaxy.Radius / 4f),
					Utils.NextGaussian(0f, g.Galaxy.Height / 3f),
					Utils.NextGaussian(0f, g.Galaxy.Radius / 4f)
					);
			} while (Vector3.Distance(pos, Vector3.zero) < g.Galaxy.Radius / 6f);
			g.Position = pos;

			gateNetwork.Add(g);
		}

		// Make arms
		float deg = 360f / 4f;
		for (int arm = 0; arm < 4; arm++)
		{
			float armPos = deg * arm; // Angle rotation around center
			for (int node = 0; node < 7; node++)
			{
				float wiggle = (2.0f + Random.value - 1.0f) * (galaxies[0].Height / 6f) ;
				Vector3 nodePos = Quaternion.AngleAxis(armPos + wiggle, Vector3.up) * Vector3.one * (galaxies[0].Radius / 4f + (galaxies[0].Radius / 4f * node));//((node + galaxies[0].Radius / 4f) * 4); // Position of node
				for (int i = 0; i < 100; i++)
				{
					Gate g = new Gate() { Galaxy = galaxies[0] };
					Vector3 pos = new Vector3(
						Utils.NextGaussian(0f, g.Galaxy.Radius / (3f)) + nodePos.x,
						Utils.NextGaussian(0f, g.Galaxy.Height / 4f),
						Utils.NextGaussian(0f, g.Galaxy.Radius / (3f)) + nodePos.z
						);
					g.Position = pos;
					gateNetwork.Add(g);
				}
			}
		}

		// Broad pass
		for (int i = 0; i < 1200; i++)
		{
			Gate g = new Gate() { Galaxy = galaxies[0] };
			Vector3 pos = new Vector3(
						Utils.NextGaussian(0f, g.Galaxy.Radius * 0.9f),
						Utils.NextGaussian(0f, g.Galaxy.Height * 0.3f),
						Utils.NextGaussian(0f, g.Galaxy.Radius * 0.9f)
						);
			g.Position = pos;
			gateNetwork.Add(g);
		}

		// Twirl arms
		float true_radius = 0;

		foreach (Gate g in gateNetwork)
		{
			Vector3 pos = g.Position;
			Vector2 point = new Vector2(pos.x, pos.z);
			float distance = point.magnitude;
			float move_by_factor = rotationCurve.Evaluate(distance / g.Galaxy.Radius);
			float move_by = (Mathf.PI * 4) * move_by_factor;
			Vector3 adjusted = Quaternion.AngleAxis(Mathf.Pow(distance, 0.52f) * move_by, Vector3.up) * pos; // Creates rotation, multiplies the result by position to modify its rotation
			
			g.Position = adjusted;
			if (adjusted.magnitude > true_radius)
				true_radius = adjusted.magnitude;
		}

		//Debug.Log(true_radius.ToString());

		// Clean up
		int c = gateNetwork.Count / 3;
		for (int i = 0; i < c; i++)
			gateNetwork.RemoveAt(Random.Range(0, gateNetwork.Count));

		Gate last = new Gate() { Galaxy = galaxies[0] };
		last.Position = new Vector3(60.3f, -20f, 60.7f);
		gateNetwork.Add(last);

		galaxies[0].Gates = gateNetwork;
	}

	public int[] CalculateAddress(Gate gate)
	{
		//Check if given gate has a saved static address and use that
		//Figure out where it is from the center
		//Determine if we need a reference point
			// If so, pick a spot to put it, preferring as close as possible to it
		//Store information in the address array
		//If the gate doesn't have a saved static address, store it
		if (gate.StaticAddress != null)
			return gate.StaticAddress;

		int[] address = new int[] { -1, -1, -1, -1, -1, -1 };
		float allowance = baseRadius / maxVal;

		Vector3 gatePos = gate.Position; // Position of the target gate
		
		Quaternion gateAngle = Quaternion.FromToRotation(Vector3.up, gatePos);
		//Debug.Log("gateAngle: " + gateAngle.ToString() + "-euler: " + gateAngle.eulerAngles.ToString() + " normalized: " + gateAngle.normalized.ToString());
		Quaternion gridAngle = Quaternion.Euler(
			Utils.SnapToGrid(gateAngle.eulerAngles.x, nPerDegree),
			Utils.SnapToGrid(gateAngle.eulerAngles.y, nPerDegree),
			Utils.SnapToGrid(gateAngle.eulerAngles.z, nPerDegree)
			);
		//Debug.Log("gridAngle: " + gridAngle.ToString() + "-euler: " + gridAngle.eulerAngles.ToString() + "normalized: " + gridAngle.normalized.ToString());
		float gateMagnitude = gatePos.magnitude; // Length of vector
		float gridMagnitude = Utils.SnapToGrid(gateMagnitude, nPerDist); // Snap length to grid to know where we need to check from
		allowance = Mathf.Sin((baseRadius / 2) * Mathf.Deg2Rad) * gridMagnitude; // Adjust allowance for the length of the current vector, to make the radius bigger the farther away the target is

		//Debug.Log(gridAngle.ToString());
		Vector3 gridPos = gridAngle * Vector3.up * gridMagnitude;

		//Debug.LogWarning("Checking " + gridPos.ToString() + " against " + gatePos.ToString());
		if (Vector3.Distance(gridPos, gatePos) <= allowance)
		{
			// Convert grid position into azimuth/altitude/magnitude baseN integers, return them
			gate.inRange = true;

			/*
			if (gatePos == new Vector3(60.3f, -20f, 60.7f))
			{
				Debug.Log("Final One");
			}
			*/
			float azimuth = Mathf.Atan2(gridPos.z, gridPos.x) * Mathf.Rad2Deg;// * Mathf.Rad2Deg;
			float altitude = Mathf.Asin(gridPos.y / gridPos.magnitude) * Mathf.Rad2Deg;

			int azimuthN = Mathf.RoundToInt(azimuth / nPerDegree + maxVal / 2);
			int altitudeN = Mathf.RoundToInt(altitude / (nPerDegree * 2) + maxVal / 2);
			int magnitudeN = Mathf.RoundToInt(gridMagnitude / nPerDist);

			address[3] = azimuthN;
			address[4] = altitudeN;
			address[5] = magnitudeN;

			if (Application.isEditor)
				Debug.Log(string.Join(", ", address.Select(i => i.ToString()).ToArray()));

			
			float y = gridMagnitude * Mathf.Sin(altitude * Mathf.Deg2Rad);
			float hy = gridMagnitude * Mathf.Cos(altitude * Mathf.Deg2Rad);
			float z = hy * Mathf.Sin(azimuth * Mathf.Deg2Rad);
			float x = z / Mathf.Tan(azimuth * Mathf.Deg2Rad);

			Vector3 backform = new Vector3(x, y, z); // Difference is around 8 * 10^-8, which is good enough

			Debug.Log("Actual: " + gatePos.ToString("G8") + ";\nGrid: " + gridPos.ToString("G8") + ";\nDecoded: " + backform.ToString("G8") + ";\nDistance b/w: " + Vector3.Distance(gridPos, backform).ToString());
			Debug.LogWarning((azimuth) + ", " + (altitude) + ", " + (gridMagnitude) +"\n" + (gridPos.x - backform.x).ToString("G8") + ", " + (gridPos.y - backform.y).ToString("G8") + ", " + (gridPos.z - backform.z).ToString("G8"));
		}
		else
		{
			gate.inRange = false;
			//Debug.Log(gridPos);
			// CRAP WE NEED ONE
			// Recalculate vector assuming the 2D grid position is zero, convert to baseN and return reference point + desination
		}

		address[0] = 0; //Galaxy

		gate.GridPosition = gridPos;
		
		return address;
	}

	public Gate ExtractFromAddress(int[] address)
	{
		// Get values from address
		int galaxy = address[0];
		int ref_azimuthN = address[1];
		int ref_magnitudeN = address[2];
		int azimuthN = address[3];
		int altitudeN = address[4];
		int magnitudeN = address[5];

		float azimuth, altitude, magnitude;

		// Do some extra things if reference exists
		if (ref_azimuthN != -1 || ref_magnitudeN != -1)
		{
			float ref_azimuth, ref_magnitude;
			ref_azimuth = azimuthN * nPerDegree - maxVal / 2;
			ref_magnitude = magnitudeN * nPerDist;
		}

		// Reverse operations to get proper values that the engine likes
		azimuth = (float)azimuthN * nPerDegree - 360 / 2;
		altitude = (float)altitudeN * (nPerDegree * 2) - 360;
		magnitude = (float)magnitudeN * nPerDist;

		float y = magnitude * Mathf.Sin(altitude * Mathf.Deg2Rad);
		float hy = magnitude * Mathf.Cos(altitude * Mathf.Deg2Rad);
		float z = hy * Mathf.Sin(azimuth * Mathf.Deg2Rad);
		float x = z / Mathf.Tan(azimuth * Mathf.Deg2Rad);

		float precision = 1f / 10000f;
		//float precision = 1f;
		Vector3 c = new Vector3(60.3f, -20f, 60.7f);
		Vector3 check = new Vector3(Utils.SnapToGrid(x, precision), Utils.SnapToGrid(y, precision), Utils.SnapToGrid(z, precision)); // Difference is around 8 * 10^-8 without lopping off the end, which is good enough
		float allowance = Mathf.Sin((nPerDegree) * Mathf.Deg2Rad) * magnitude;
		//float allowance = 100f;

		SortedList<float,Gate> valid_gates = new SortedList<float, Gate>();
		//float[] valid_gates = new float[0];

		foreach (Gate g in galaxies[0].Gates)
		{
			float dist = Vector3.Distance(g.Position, check);
			if (dist <= allowance)
				valid_gates.Add(dist, g);
		}

		if (valid_gates.Count > 0)
		{
			Debug.Log(azimuth + ", " + altitude + ", " + magnitude);
			Gate result = valid_gates.Values.First();
			Debug.Log(result);
			return result;
		}

		else return null;
	}

	public Gate CreateAddress(float azimuth, float altitude, float magnitude)
	{
		Gate gate = new Gate() { Galaxy = galaxies[0] };

		float y = magnitude * Mathf.Sin(altitude * Mathf.Deg2Rad);
		float hy = y / Mathf.Tan(altitude * Mathf.Deg2Rad);
		float z = hy * Mathf.Sin(azimuth * Mathf.Deg2Rad);
		float x = z / Mathf.Tan(azimuth * Mathf.Deg2Rad);
		Vector3 backform = new Vector3(x, y, z);

		Debug.Log("Test angle position: " + backform);

		gate.Position = backform;

		return gate;
	}

	private void Start()
	{
		galaxies.Add(new Galaxy() { Radius = 300f, Height = 100f });
		maxVal = Mathf.RoundToInt(Mathf.Pow(baseN, numDigits));
		nPerDegree = 360f / maxVal;
		nPerDist = (galaxies[0].Radius * 6) / maxVal;

		GenerateNetwork();

		foreach (Gate g in galaxies[0].Gates)
			CalculateAddress(g);

		//Gate gate = CreateAddress(30f, 30f, 150f);
		//CalculateAddress(gate);
		//galaxies[0].Gates.Add(gate);

		ExtractFromAddress(new int[] { 0, -1, -1, 640, 493, 50 });
	}

	private void Update()
	{
		//CalculateAddress(gateNetwork[0]);
		if (Input.GetKeyDown(KeyCode.Space))
		{
			galaxies.Clear();
			galaxies[0].Gates.Clear();

			GenerateNetwork();
			foreach (Gate g in galaxies[0].Gates)
				CalculateAddress(g);
		}

		if (Input.GetKeyDown(KeyCode.W))
		{

			easy_gates = 0;
			foreach (Gate g in galaxies[0].Gates)
				if (g.inRange)
					easy_gates++;

			Debug.Log(galaxies[0].Gates.Count + ", num close: " + easy_gates);
			CalculateAddress(galaxies[0].Gates[galaxies[0].Gates.Count - 1]);
		}

		if (Input.GetKeyDown(KeyCode.LeftArrow))
		{
			Gate g = galaxies[0].Gates[galaxies[0].Gates.Count - 1];
			Vector3 p = g.Position;
			p = Quaternion.AngleAxis(-5f, Vector3.up).normalized * p;
			g.Position = p;
			CalculateAddress(g);
		}

		if (Input.GetKeyDown(KeyCode.RightArrow))
		{
			Gate g = galaxies[0].Gates[galaxies[0].Gates.Count - 1];
			Vector3 p = g.Position;
			p = Quaternion.AngleAxis(5f, Vector3.up).normalized * p;
			g.Position = p;
			CalculateAddress(g);
		}

		if (Input.GetKeyDown(KeyCode.UpArrow))
		{
			Gate g = galaxies[0].Gates[galaxies[0].Gates.Count - 1];
			Vector3 p = g.Position;
			p = Quaternion.AngleAxis(-5f, Vector3.forward).normalized * p;
			g.Position = p;
			CalculateAddress(g);
		}

		if (Input.GetKeyDown(KeyCode.DownArrow))
		{
			Gate g = galaxies[0].Gates[galaxies[0].Gates.Count - 1];
			Vector3 p = g.Position;
			p = Quaternion.AngleAxis(5f, Vector3.forward).normalized * p;
			g.Position = p;
			CalculateAddress(g);
		}
	}

	private void OnDrawGizmos()
	{
		if (Application.isPlaying)
		{
			foreach (Gate g in galaxies[0].Gates)
			{
				//Gizmos.color = Color.gray;
				//Gizmos.DrawLine(Vector3.zero, g.GridPosition);
				Gizmos.color = Color.white;
				//Gizmos.DrawWireCube(g.Position, Vector3.one);
				if (g.inRange)
				{
					Gizmos.color = Color.green;
					Gizmos.DrawWireSphere(g.GridPosition, 2f);
				}
				else
				{
					Gizmos.color = Color.red;
					Gizmos.DrawWireSphere(g.GridPosition, 0.5f);
				}
			}

			Gate glast = galaxies[0].Gates[galaxies[0].Gates.Count - 1];

			Gizmos.color = Color.white;
			Gizmos.DrawLine(Vector3.zero, glast.Position);
			Gizmos.DrawWireSphere(glast.Position, 2f);

			Handles.color = Color.gray;
			for (int i = 0; i < 360 / 15; i++)
			{
				Handles.DrawWireDisc(Vector3.zero, Vector3.up, galaxies[0].Radius * 4f);
				Handles.DrawLine(Vector3.zero, Quaternion.AngleAxis(15 * i, Vector3.up) * Vector3.right * galaxies[0].Radius * 5f);
			}
		}
	}

	private void OnDrawGizmosSelected()
	{
		foreach (Gate g in galaxies[0].Gates)
		{
			Gizmos.color = Color.white;
			Gizmos.DrawWireCube(g.Position, Vector3.one);
		}
	}
}
