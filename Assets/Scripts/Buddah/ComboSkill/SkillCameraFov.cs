using System;
using System.Collections;
using UnityEngine;

internal static class SkillCameraFov
{
    internal static IEnumerator Run(Func<PlayerCamera> getCamera, Action onComplete,
        float fovOffset, float rampInSeconds, float durationSeconds, float settleSeconds,
        AnimationCurve rampInCurve, AnimationCurve settleCurve)
    {
        if (getCamera() == null)
            yield break;

        AnimationCurve resolvedRampInCurve = rampInCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        AnimationCurve resolvedSettleCurve = settleCurve ?? AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        if (rampInSeconds <= 0f)
        {
            getCamera().SetRuntimeFieldOfViewOffset(fovOffset);
        }
        else
        {
            float safeRampInSeconds = Mathf.Max(0.001f, rampInSeconds);
            float rampInElapsed = 0f;
            while (rampInElapsed < safeRampInSeconds)
            {
                if (getCamera() == null)
                    yield break;

                float normalizedTime = Mathf.Clamp01(rampInElapsed / safeRampInSeconds);
                float curveValue = Mathf.Clamp01(resolvedRampInCurve.Evaluate(normalizedTime));
                getCamera().SetRuntimeFieldOfViewOffset(fovOffset * curveValue);
                rampInElapsed += Time.deltaTime;
                yield return null;
            }

            if (getCamera() != null)
                getCamera().SetRuntimeFieldOfViewOffset(fovOffset);
        }

        if (durationSeconds > 0f)
            yield return new WaitForSeconds(durationSeconds);

        if (settleSeconds <= 0f)
        {
            if (getCamera() != null)
                getCamera().SetRuntimeFieldOfViewOffset(0f);
        }
        else
        {
            float safeSettleSeconds = Mathf.Max(0.001f, settleSeconds);
            float elapsed = 0f;
            while (elapsed < safeSettleSeconds)
            {
                if (getCamera() == null)
                    yield break;

                float normalizedTime = Mathf.Clamp01(elapsed / safeSettleSeconds);
                float curveValue = Mathf.Clamp01(resolvedSettleCurve.Evaluate(normalizedTime));
                getCamera().SetRuntimeFieldOfViewOffset(fovOffset * curveValue);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (getCamera() != null)
            getCamera().SetRuntimeFieldOfViewOffset(0f);

        onComplete();
    }
}
