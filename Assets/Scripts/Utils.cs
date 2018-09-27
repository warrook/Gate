using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utils
{
	public static float NextGaussian()
	{
		float v1, v2, s;
		do
		{
			v1 = 2.0f * Random.value - 1.0f;
			v2 = 2.0f * Random.value - 1.0f;
			s = v1 * v1 + v2 * v2;
		} while (s >= 1.0f || s == 0f); // s: sigma

		s = Mathf.Sqrt((-2.0f * Mathf.Log(s)) / s);

		return v1 * s;
	}

	public static float NextGaussian(float mean, float standard_deviation)
	{
		return mean + NextGaussian() * standard_deviation;
	}

	public static float NextGaussian(float mean, float standard_deviation, float min, float max)
	{
		float x;
		do
			x = NextGaussian(mean, standard_deviation);
		while (x < min || x > max);
		return x;
	}

	public static float SnapToGrid(float value, float snapValue)
	{
		return Mathf.Round(value / snapValue) * snapValue;
	}
}
