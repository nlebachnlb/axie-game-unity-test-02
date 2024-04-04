using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    public class AIController : MonoBehaviour
    {
        /// <summary>
        /// This class contains a calculated shot result
        /// </summary>
        private class ShotResult
        {
            public float InitialVelocity { get; set; }
            public float AngleInDegree { get; set; }
            public float LandingTimeSeconds { get; set; }
            public float BulletSpeed { get; set; }

            public override string ToString()
            {
                return $"(v0={InitialVelocity}, angle={AngleInDegree}, t={LandingTimeSeconds}, speed={BulletSpeed}";
            }
        }

        private DefenseState defenseState;

        private void Awake()
        {
            var manager = FindObjectOfType<GameDefenseManager>();
            if (manager == null)
            {
                this.enabled = false;
            }
            else
            {
                defenseState = manager.GetState();
            }
        }

        private void Update()
        {
            if (defenseState == null || !defenseState.isPlaying || defenseState.playerState.shootDelay > 0) return;

            EnemyState nearestEnemy = FindTarget();
            if (nearestEnemy == null) return;

            Vector2 predictedPos = PredictTargetPosition(nearestEnemy.pos - defenseState.playerState.pos, nearestEnemy.speed);
            ShotResult shot = CalculateShotPath(predictedPos);
            Debug.Log("Shot result: " + shot);

            if (defenseState.energy >= DefenseState.ENERGY_SHOT_MAX_CHARGE)
                defenseState.DoShootSpecial(shot.AngleInDegree, shot.InitialVelocity);
            else
                defenseState.DoShoot(shot.AngleInDegree, shot.InitialVelocity);
        }

        private EnemyState FindTarget()
        {
            // Focus on fastest enemies
            var maxSpeed = defenseState.enemyStates.Values.Aggregate(0f, (current, enemy) => Mathf.Max(current, enemy.speed));
            var candidates = defenseState.enemyStates.Values.Where(e => e.speed >= maxSpeed).ToList();
            if (candidates.Count == 1) return candidates[0];
            
            // Then focus on nearest enemies
            EnemyState nearestEnemy = null;
            float nearestDist = -1;
            foreach (var enemy in candidates)
            {
                if (nearestEnemy != null && !(enemy.pos.x < nearestDist)) continue;
                nearestDist = enemy.pos.x;
                nearestEnemy = enemy;
            }
            return nearestEnemy;
        }

        /// <summary>
        /// This method determines angle and power to help Axie shoot exactly at a target position
        /// </summary>
        /// <param name="target"> Target's position RELATIVE to Axie (not world position) </param>
        /// <returns></returns>
        private ShotResult CalculateShotPath(Vector2 target)
        {
            var (tx, ty) = (target.x, target.y);
            var g = DefenseState.GRAVITY;
            var angle = DetermineAngle(target);
            var power = DeterminePower(target, angle);
            var bulletSpeed = DefenseState.POWER_MIN + DefenseState.POWER_BOOST_MAX * Mathf.Clamp01(power * 0.01f);
            var time = tx / (bulletSpeed * Mathf.Cos(angle * Mathf.Deg2Rad));
            return new ShotResult()
            {
                AngleInDegree = angle,
                InitialVelocity = power,
                LandingTimeSeconds = time,
                BulletSpeed = bulletSpeed
            };
        }

        /// <summary>
        /// This method helps Axie with prediction of enemy's position
        /// </summary>
        /// <param name="target"> Target's position RELATIVE to Axie (not world position)</param>
        /// <param name="vx"> Horizontal velocity to calculate prediction of next position</param>
        /// <returns></returns>
        private Vector2 PredictTargetPosition(Vector2 target, float vx)
        {
            float bestDiff = Mathf.Infinity;
            Vector2 pos = target;
            Vector2 ret = target + Vector2.left * (vx * DefenseState.FIXED_TIME_STEP);
            
            while (pos.x > -vx * DefenseState.FIXED_TIME_STEP)
            {
                pos.x -= vx * DefenseState.FIXED_TIME_STEP;
                ShotResult result = CalculateShotPath(pos);
                var futurePos = target + Vector2.left * (vx * result.LandingTimeSeconds);
                if (Mathf.Abs(futurePos.x - pos.x) < bestDiff)
                {
                    bestDiff = Mathf.Abs(futurePos.x - pos.x);
                    ret = futurePos;
                }
            }

            // Debug.Log("Accurate at: " + ret.x + "," + ret.y);
            return ret;
        }
        
        // Determine power to shoot bullet at target position relative to ROOT COORDINATE (0, 0)
        private float DeterminePower(Vector2 target, float angleDegree)
        {
            var x = target.x;
            var y = target.y;
            var angle = angleDegree * Mathf.Deg2Rad;
            var g = DefenseState.GRAVITY;
            var v0 = Mathf.Sqrt((Mathf.Pow(x, 2) * g) /
                                   (2 * x * Mathf.Sin(angle) * Mathf.Cos(angle) -
                                    2 * y * Mathf.Pow(Mathf.Cos(angle), 2)));
            return Mathf.Min(v0 * 10f, 100);
        }
        
        // Determine angle to shoot bullet at target position relative to ROOT COORDINATE (0, 0)
        private float DetermineAngle(Vector2 target)
        {
            // I see, there is no constraint of min and max height of the bullet path, that means no limit in angle
            // So I can freely determine the angle mua ha ha :)
            // (I tried doing this myself by holding left mouse and there is no limit indeed)
            // So "height" can be negative or positive
            float height = target.y + target.magnitude / 2f;
            float sqrt = Mathf.Sqrt(2 * DefenseState.GRAVITY * Mathf.Abs(height)) * Mathf.Sign(height);
            
            float angle = Mathf.Atan2(sqrt, target.x);
            float degreeAngle = angle * Mathf.Rad2Deg;
            return degreeAngle;
        }
    }
}
