        // Pure request-direction math only. Force magnitude and validated contact dispatch remain Player-owned.
        static bool TryDirectedPushDirection(Vector3 surfaceNormal, Vector3 travelIntent, float minimumAwayDot, out Vector3 direction)
        {
            direction = Vector3.zero;
            float normalSquared = surfaceNormal.sqrMagnitude, intentSquared = travelIntent.sqrMagnitude;
            if (!Finite(surfaceNormal) || !Finite(travelIntent) || !Finite(minimumAwayDot)
                || !Finite(normalSquared) || !Finite(intentSquared) || normalSquared < .0001f || intentSquared < .0001f) return false;
            Vector3 normal = surfaceNormal / Mathf.Sqrt(normalSquared), intent = travelIntent / Mathf.Sqrt(intentSquared);
            float away = Mathf.Clamp(Mathf.Max(.25f, minimumAwayDot + .025f), 0f, 1f);
            float outward = Vector3.Dot(intent, normal);
            if (outward >= away) { direction = intent; return true; }
            Vector3 tangent = intent - normal * outward;
            float tangentLength = tangent.magnitude;
            if (tangentLength == 0f) { direction = normal; return true; }
            Vector3 tangentDirection = tangent / tangentLength;
            if (tangentLength >= .25f)
                direction = normal * away + tangentDirection * Mathf.Sqrt(1f - away * away);
            else
            {
                // A hard .25 cutoff would jump75.52 degrees at the default cone. Blend the angle instead.
                float ratio = Mathf.Clamp01(tangentLength / .25f);
                float angle = ratio * ratio * (3f - 2f * ratio) * Mathf.Acos(away);
                direction = normal * Mathf.Cos(angle) + tangentDirection * Mathf.Sin(angle);
            }
            return true;
        }

