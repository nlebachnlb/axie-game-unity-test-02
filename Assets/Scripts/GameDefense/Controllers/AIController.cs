using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class AIController : MonoBehaviour
    {
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
        
        DefenseState _defenseState;
        int _shootAngle = 45;
        int _power = 0;

        private Dictionary<int, List<(float, float, float)>> prediction = new Dictionary<int, List<(float, float, float)>>();

        private void Awake()
        {
            var manager = FindObjectOfType<GameDefenseManager>();
            if (manager == null)
            {
                this.enabled = false;
            }
            else
            {
                _defenseState = manager.GetState();
            }

            CalculateTimePrediction();
        }

        private void CalculateTimePrediction()
        {
            // for (int enemyType = 0; enemyType < 3)
        }

        // Update is called once per frame
        void Update()
        {
            if (_defenseState == null || !_defenseState.isPlaying || _defenseState.playerState.shootDelay > 0) return;

            EnemyState nearestEnemy = FindTarget();
            if (nearestEnemy == null) return;

            Vector2 predictedPos = PredictTargetPosition(nearestEnemy.pos, nearestEnemy.speed);
            ShotResult shot = CalculateShotPath(predictedPos);
            Debug.Log("Shot result: " + shot);

            if (_defenseState.energy >= DefenseState.ENERGY_SHOT_MAX_CHARGE)
            {
                _defenseState.DoShootSpecial(shot.AngleInDegree, shot.InitialVelocity);
            }
            else
            {
                _defenseState.DoShoot(shot.AngleInDegree, shot.InitialVelocity);
            }
        }

        EnemyState FindTarget()
        {
            EnemyState nearestEnemy = null;
            float nearestDist = -1;
            foreach (var p in _defenseState.enemyStates)
            {
                var enemy = p.Value;
                if (nearestEnemy == null || enemy.pos.x < nearestDist)
                {
                    nearestDist = enemy.pos.x;
                    nearestEnemy = enemy;
                }
            }
            return nearestEnemy;
        }

        private ShotResult CalculateShotPath(Vector2 target)
        {
            var (tx, ty) = (target.x, target.y);
            var g = DefenseState.GRAVITY;
            
            float height = target.y + target.magnitude / 2f;
            var h = Mathf.Max(0.01f, height);

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

        private Vector2 PredictTargetPosition(Vector2 target, float vx)
        {
            Vector2 pos = target;
            float bestDiff = 11111f;
            Vector2 ret = target + Vector2.left * (vx * DefenseState.FIXED_TIME_STEP);
            
            while (pos.x > -vx)
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

            Debug.Log("Accurate at: " + ret.x + "," + ret.y);
            return ret;
        }
        
        private int DeterminePower(Vector2 target, float angleDegree)
        {
            var x = target.x;
            var y = target.y;
            var angle = angleDegree * Mathf.Deg2Rad;
            var g = DefenseState.GRAVITY;
            var vZero = Mathf.Sqrt((Mathf.Pow(x, 2) * g) /
                                   (2 * x * Mathf.Sin(angle) * Mathf.Cos(angle) -
                                    2 * y * Mathf.Pow(Mathf.Cos(angle), 2)));
            return (int) Mathf.Min(vZero * 10f, 100);
        }
        
        private float DetermineAngle(Vector2 target)
        {
            float height = target.y + target.magnitude / 2f;
            height = Mathf.Max(0.01f, height);
            
            float angle = Mathf.Atan2(Mathf.Sqrt(2 * DefenseState.GRAVITY * height), target.x);
            float degreeAngle = angle * Mathf.Rad2Deg;
            return degreeAngle;
        }
    }
}
