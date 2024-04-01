using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace Game
{
    public interface IRectCollider
    {
        public Vector2 pos { get; }
        public Vector2 size { get; }
    }

    public enum CollisionResult
    {
        None,
        Left,
        Right,
        Bottom,
        Top
    }

    public class EnemyState : IRectCollider
    {
        public int id;
        public bool isActivated;
        public int enemyType;
        public int hp;
        public float speed;
        public Vector2 pos { get; set; }
        public Vector2 size => new Vector2(colliderRad * 2, colliderRad * 2);
        public float colliderRad;
    }

    public class BulletState : IRectCollider
    {
        public int id;
        public bool isActivated;
        public int atk;
        public float speed;
        public Vector2 pos { get; set; }
        public Vector2 vel;
        public float colliderRad;
        public Vector2 size => new Vector2(colliderRad * 2, colliderRad * 2);
    }

    public class PlayerState
    {
        public float shootDelay;
        public Vector2 pos;
    }

    public class WaveState
    {
        public float delay;
        public int enemyType;
        public int amount;
    }

    public class DefenseState
    {
        public const float FIXED_TIME_STEP = 0.02f;
        public const float GRAVITY = 2.8f;
        public const float POWER_BOOST_MAX = 36f;
        public const float POWER_MIN = 4f;
        public const float SHOOT_DELAY_TIME = 0.5f;
        public const int ENERGY_SHOT_MAX_CHARGE = 10;
        public const int FUR_BALL_DELTA_ANGLE = 10;
        public const int BULLET_ATK = 3;

        public static readonly int[] ENEMY_HPS = new[] { 3, 5, 10 };
        public static readonly float[] ENEMY_SPEEDS = new[] { 4f, 2f, 1f };

        public bool isPlaying { get; private set; } = false;
        public List<WaveState> waveStates { get; private set; } = new List<WaveState>();
        public Dictionary<int, EnemyState> enemyStates { get; private set; } = new Dictionary<int, EnemyState>();
        public Dictionary<int, BulletState> bulletStates { get; private set; } = new Dictionary<int, BulletState>();
        public PlayerState playerState { get; private set; } = new PlayerState();
        public int nextWave { get; private set; }
        public float waveDelay { get; private set; }
        private int bulletCount;
        private int enemyCount;
        public int turn { get; private set; }
        public int score { get; private set; }
        public int energy { get; private set; }
        public bool isEnded {
            get
            {
                return nextWave >= waveStates.Count && enemyStates.Count == 0;
            }
        }

        public JArray jBullets;

        private UnityAction<Vector2, Vector2> _onHitObjectEvent;

        public void Init(UnityAction<Vector2, Vector2> onHitObjectEvent)
        {
            this._onHitObjectEvent = onHitObjectEvent;

            jBullets = new JArray();
            playerState.pos = new Vector2(0, 5);
            playerState.shootDelay = 0;

            turn = 0;
            score = 0;
            bulletCount = 0;
            enemyCount = 0;
            energy = 0;

            enemyStates.Clear();
            bulletStates.Clear();
            waveStates.Clear();
            for (int i = 0;i < 20;i++)
            {
                int ranVal = Random.Range(0, 5);
                int enemyType;
                int amount;
                float delay;
                if (ranVal == 0)
                {
                    enemyType = 2;
                    amount = Random.Range(1, 3);
                    delay = amount * 5;
                }
                else if (ranVal == 1 || ranVal == 2)
                {
                    enemyType = 0;
                    amount = Random.Range(2, 4);
                    delay = amount * 3;
                }
                else
                {
                    enemyType = 1;
                    amount = Random.Range(3, 6);
                    delay = amount * 3;
                }

                WaveState waveState = new WaveState
                {
                    delay = delay,
                    enemyType = enemyType,
                    amount = amount
                };
                waveStates.Add(waveState);
            }

            nextWave = 0;
            waveDelay = 1f;
            isPlaying = true;
        }

        public void OnUpdate()
        {
            turn++;
            if (playerState.shootDelay > 0)
            {
                playerState.shootDelay -= FIXED_TIME_STEP;
            }
            if (nextWave < waveStates.Count)
            {
                if (waveDelay > 0 && enemyStates.Count > 0)
                {
                    waveDelay -= FIXED_TIME_STEP;
                }
                else
                {
                    SpawnNextWave();
                }
            }

            foreach (var p in bulletStates)
            {
                var bulletState = p.Value;
                bulletState.vel = new Vector2(bulletState.vel.x, bulletState.vel.y - GRAVITY * FIXED_TIME_STEP);
                bulletState.pos = bulletState.pos + bulletState.vel * bulletState.speed * FIXED_TIME_STEP;

                if (bulletState.pos.y < 0)
                {
                    bulletState.isActivated = false;
                }
                else
                {
                    foreach (var e in enemyStates)
                    {
                        var colResult = CollisionCheck(bulletState, e.Value);
                        if (colResult != CollisionResult.None)
                        {
                            bulletState.isActivated = false;
                            int atk = e.Value.hp < bulletState.atk ? e.Value.hp : bulletState.atk;
                            e.Value.hp -= atk;

                            score += atk;

                            Vector2 bulletCenter = bulletState.pos + new Vector2(0f, bulletState.colliderRad);
                            _onHitObjectEvent?.Invoke(bulletCenter, bulletState.vel);
                        }
                    }
                    if (!bulletState.isActivated)
                    {
                        energy = Mathf.Min(ENERGY_SHOT_MAX_CHARGE, energy + 1);
                    }
                }
            }

            foreach (var p in enemyStates)
            {
                var enemyState = p.Value;
                enemyState.pos = new Vector2(enemyState.pos.x - enemyState.speed * FIXED_TIME_STEP, enemyState.pos.y);
                if (enemyState.hp <= 0)
                {
                    enemyState.isActivated = false;
                }
                else if (enemyState.pos.x < 0)
                {
                    enemyState.isActivated = false;
                    score -= enemyState.hp;
                }
            }

            enemyStates
                .Where(x => !x.Value.isActivated)
                .Select(x => x.Key).ToList()
                .ForEach(id => enemyStates.Remove(id));

            bulletStates
                .Where(x => !x.Value.isActivated)
                .Select(x => x.Key).ToList()
                .ForEach(id => bulletStates.Remove(id));
        }

        CollisionResult CollisionCheck(IRectCollider objA, IRectCollider objB)
        {
            var vX = objA.pos.x - (objB.pos.x);
            var vY = objA.pos.y + objA.size.y / 2 - (objB.pos.y + objB.size.y / 2);
            var hWidths = objA.size.x / 2 + objB.size.x / 2;
            var hHeights = objA.size.y / 2 + objB.size.y / 2;
            CollisionResult collisionDirection = CollisionResult.None;
            if (Mathf.Abs(vX) < hWidths && Mathf.Abs(vY) < hHeights)
            {
                var offsetX = hWidths - Mathf.Abs(vX);
                var offsetY = hHeights - Mathf.Abs(vY);

                if (Mathf.Ceil(offsetX) >= Mathf.Ceil(offsetY))
                {
                    if (vY > 0 && vY < 1)
                    {
                        collisionDirection = CollisionResult.Bottom;
                    }
                    else if (vY < 0)
                    {
                        collisionDirection = CollisionResult.Top;
                    }
                }
                else
                {
                    if (vX > 0)
                    {
                        collisionDirection = CollisionResult.Left;
                    }
                    else
                    {
                        collisionDirection = CollisionResult.Right;
                    }
                }
            }
            return collisionDirection;
        }

        private void SpawnNextWave()
        {
            WaveState waveState = waveStates[nextWave];
            nextWave++;
            waveDelay = waveState.delay;

            List<int> offsets = new List<int>();
            for (int i = 0;i < 9;i++)
            {
                offsets.Add(i);
            }
            for (int i = 0;i < waveState.amount;i++)
            {
                EnemyState enemyState = new EnemyState();
                enemyState.id = enemyCount++;
                enemyState.isActivated = true;
                enemyState.enemyType = waveState.enemyType;
                int ranIdx = Random.Range(0, offsets.Count);
                int dx = offsets[ranIdx] % 3;
                int dy = offsets[ranIdx] / 3;
                offsets.Remove(ranIdx);
                if (waveState.enemyType == 0)
                {
                    enemyState.colliderRad = 0.5f;
                    enemyState.pos = new Vector2(20 + dx, dy);
                }
                else if (waveState.enemyType == 1)
                {
                    enemyState.colliderRad = 0.5f;
                    enemyState.pos = new Vector2(20 + dx, dy);
                }
                else
                {
                    enemyState.colliderRad = 1f;
                    enemyState.pos = new Vector2(20 + dx, 2 + dy);
                }
                enemyState.hp = ENEMY_HPS[waveState.enemyType];
                enemyState.speed = ENEMY_SPEEDS[waveState.enemyType] + nextWave * 0.02f;
                
                enemyStates.Add(enemyState.id, enemyState);
            }
        }

        public bool DoShoot(float angle, float power)
        {
            if (playerState.shootDelay > 0) return false;
            playerState.shootDelay = SHOOT_DELAY_TIME;
            float vx = Mathf.Cos(angle * Mathf.Deg2Rad);
            float vy = Mathf.Sin(angle * Mathf.Deg2Rad);

            BulletState bulletState = new BulletState();
            bulletState.id = bulletCount++;
            bulletState.isActivated = true;
            bulletState.atk = BULLET_ATK;
            bulletState.speed = POWER_MIN + POWER_BOOST_MAX * Mathf.Clamp01(power * 0.01f);
            bulletState.colliderRad = 0.1f;
            bulletState.pos = new Vector2(playerState.pos.x, playerState.pos.y);
            bulletState.vel = new Vector2(vx, vy);
            bulletStates.Add(bulletState.id, bulletState);

            JObject jBullet = new JObject();
            jBullet.Add("turn", turn);
            jBullet.Add("type", 0);
            jBullet.Add("angle", angle);
            jBullet.Add("power", power);
            jBullets.Add(jBullet);

            return true;
        }

        public bool DoShootSpecial(float angle, float power)
        {
            if (energy < ENERGY_SHOT_MAX_CHARGE) return false;
            energy = 0;
            for (int i = -1;i <= 1;i++)
            {
                float vx = Mathf.Cos((angle + i * FUR_BALL_DELTA_ANGLE) * Mathf.Deg2Rad);
                float vy = Mathf.Sin((angle + i * FUR_BALL_DELTA_ANGLE) * Mathf.Deg2Rad);

                BulletState bulletState = new BulletState();
                bulletState.id = bulletCount++;
                bulletState.isActivated = true;
                bulletState.atk = BULLET_ATK;
                bulletState.speed = POWER_MIN + POWER_BOOST_MAX * Mathf.Clamp01(power * 0.01f);
                bulletState.colliderRad = 0.1f;
                bulletState.pos = new Vector2(playerState.pos.x, playerState.pos.y);
                bulletState.vel = new Vector2(vx, vy);
                bulletStates.Add(bulletState.id, bulletState);
            }

            JObject jBullet = new JObject();
            jBullet.Add("turn", turn);
            jBullet.Add("type", 1);
            jBullet.Add("angle", angle);
            jBullet.Add("power", power);
            jBullets.Add(jBullet);
            return true;
        }

        public List<Vector2> PredictBulletPath(float angle, float power)
        {
            float vx = Mathf.Cos(angle * Mathf.Deg2Rad);
            float vy = Mathf.Sin(angle * Mathf.Deg2Rad);

            Vector2 pos = new Vector2(playerState.pos.x, playerState.pos.y);
            Vector2 vel = new Vector2(vx, vy);
            float speed = POWER_MIN + POWER_BOOST_MAX * Mathf.Clamp01(power * 0.01f);

            List<Vector2> path = new List<Vector2>();
            path.Add(pos);
            int k = 0;
            while (pos.y >= 0)
            {
                vel = new Vector2(vel.x, vel.y - GRAVITY * FIXED_TIME_STEP);
                pos = pos + vel * speed * FIXED_TIME_STEP;

                if (k % 2 == 0)
                {
                    path.Add(pos);
                }

                k++;
            }
            return path;
        }
    }
}
