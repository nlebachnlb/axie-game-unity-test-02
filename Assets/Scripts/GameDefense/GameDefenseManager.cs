using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;


namespace Game
{
    public class GameDefenseManager : MonoBehaviour
    {
        [SerializeField] TMPro.TextMeshProUGUI playerNameText;
        [SerializeField] TMPro.TextMeshProUGUI playerRankText;
        [SerializeField] TMPro.TextMeshProUGUI ramainTimeText;

        [SerializeField] UILayer uiLayer;
        [SerializeField] GameObject[] sampleObjects;
        [SerializeField] AxieObject axie;
        [SerializeField] Transform root;
        [SerializeField] TMPro.TextMeshProUGUI scoreText;
        [SerializeField] Button skillButton;
        [SerializeField] TMPro.TextMeshProUGUI energyText;
        [SerializeField] GameObject skillActivatingGO;
        [SerializeField] TMPro.TextMeshProUGUI waveText;
        [SerializeField] Image waveFill;
        [SerializeField] TMPro.TextMeshProUGUI speedText;

        Camera mainCam;

        Dictionary<int, EnemyObject> enemies = new Dictionary<int, EnemyObject>();
        Dictionary<int, BulletObject> bullets = new Dictionary<int, BulletObject>();
        Dictionary<string, List<GameObject>> pools = new Dictionary<string, List<GameObject>>();
        List<GameObject> predictPath = new List<GameObject>();

        DefenseState defenseState = new DefenseState();
        public DefenseState GetState() { return defenseState; }

        float deltaTimeS;

        float accumulateTime;

        bool isHolding = false;
        float holdingTime = 0;
        bool isSkillActivating = false;

        int currentSpeedIdx = 1;
        static readonly float[] SPEEDS = new[] { 0.5f, 1f, 2f, 4f };

        private void Awake()
        {
            mainCam = Camera.main;
            skillButton.onClick.AddListener(OnSkillButtonClicked);
        }

        // Start is called before the first frame update
        void Start()
        {
            AxieMixer.Unity.Mixer.Init();
            string axieId = PlayerPrefs.GetString("selectingId", "2727");
            string genes = PlayerPrefs.GetString("selectingGenes", "0x2000000000000300008100e08308000000010010088081040001000010a043020000009008004106000100100860c40200010000084081060001001410a04406");
            axie.figure.SetGenes(axieId, genes);
            StartGame();
        }

        void StartGame()
        {
            defenseState.Init(this.OnHitObjectEvent);
            axie.transform.localPosition = new Vector3(defenseState.playerState.pos.x, defenseState.playerState.pos.y, 0);
            accumulateTime = 0;
        }

        public void OnSpeedButtonClicked()
        {
            currentSpeedIdx = (currentSpeedIdx + 1) % SPEEDS.Length;
            Time.timeScale = SPEEDS[currentSpeedIdx];
            speedText.text = $"x{SPEEDS[currentSpeedIdx]}";
        }

        public void OnSkillButtonClicked()
        {
            isHolding = false;
            if (defenseState.energy >= DefenseState.ENERGY_SHOT_MAX_CHARGE)
            {
                isSkillActivating = !isSkillActivating;
                skillActivatingGO.SetActive(isSkillActivating);
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (!defenseState.isPlaying || defenseState.isEnded)
            {
                return;
            }
            deltaTimeS += Time.unscaledDeltaTime;
            int remainS = 20 * 60 - (int)deltaTimeS;
            int mins = remainS / 60;
            int secs = remainS - mins * 60;
            if (mins < 3)
            {
                ramainTimeText.text = $"<color=red>{mins:00}m{secs:00}s</color>";
            }
            else
            {
                ramainTimeText.text = $"{mins:00}m{secs:00}s";
            }
            accumulateTime += Time.deltaTime;
            while (this.accumulateTime >= DefenseState.FIXED_TIME_STEP)
            {
                this.accumulateTime -= DefenseState.FIXED_TIME_STEP;
                defenseState.OnUpdate();
            }

            if (defenseState.isEnded)
            {
                uiLayer.gameObject.SetActive(true);
            }

            if (!isHolding)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    isHolding = true;
                    holdingTime = 0f;
                    var wPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
                    float dx = wPos.x - axie.transform.position.x;
                    float dy = wPos.y - axie.transform.position.y;
                    float angle = Mathf.Atan2(dy, dx);
                    axie.animing.transform.localEulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg);
                }
            }
            else
            {
                holdingTime += Time.deltaTime;
                if (Input.GetMouseButtonUp(0))
                {
                    isHolding = false;
                    predictPath.ForEach(x => x.SetActive(false));
                    axie.powerText.text = "";
                    // if (defenseState.energy >= DefenseState.ENERGY_SHOT_MAX_CHARGE && isSkillActivating)
                    // {
                    //     defenseState.DoShootSpecial(axie.animing.transform.localEulerAngles.z, 100 * Mathf.Clamp01(holdingTime * 2));
                    // }
                    // else
                    // {
                    //     defenseState.DoShoot(axie.animing.transform.localEulerAngles.z, 100 * Mathf.Clamp01(holdingTime * 2));
                    // }
                }
                else
                {
                    axie.powerText.text = $"{(int)(100 * Mathf.Clamp01(holdingTime * 2))}";
                    var wPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
                    float dx = wPos.x - axie.transform.position.x;
                    float dy = wPos.y - axie.transform.position.y;
                    float angle = Mathf.Atan2(dy, dx);
                    axie.animing.transform.localEulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg);

                    if (holdingTime >= 0.1f)
                    {
                        var path = defenseState.PredictBulletPath(axie.animing.transform.localEulerAngles.z, 100 * Mathf.Clamp01(holdingTime * 2));

                        predictPath.ForEach(x => x.SetActive(false));
                        predictPath.Clear();
                        for (int i = 0;i < path.Count;i++)
                        {
                            var dot = NewGameObject("DotObject");
                            dot.transform.localPosition = new Vector3(path[i].x, path[i].y, 0);
                            predictPath.Add(dot);
                        }
                    }
                }
            }

            OnSynsObjects();
        }

        GameObject NewGameObject(string type)
        {
            var sample = sampleObjects.FirstOrDefault(x => x.name == type);
            if (sample == null) return null;
            List<GameObject> lst;
            if (!pools.TryGetValue(type, out lst))
            {
                lst = new List<GameObject>();
                pools.Add(type, lst);
            }

            GameObject go;
            int freeIdx = lst.FindIndex(x => !x.activeSelf);
            if (freeIdx == -1)
            {
                go = Instantiate(sample, root);
                lst.Add(go);
            }
            else
            {
                go = lst[freeIdx];
                go.SetActive(true);
            }
            return go;
        }

        void OnHitObjectEvent(Vector2 pos, Vector2 dir)
        {
            var impactObject = NewGameObject("ImpactObject");
            impactObject.GetComponent<ImpactObject>().Show();
            impactObject.transform.localPosition = new Vector3(pos.x, pos.y, 0);
            float angle = Mathf.Atan2(dir.y, dir.x);
            impactObject.transform.localEulerAngles = new Vector3(0f, 0f, angle * Mathf.Rad2Deg);
        }

        void OnSynsObjects()
        {
            scoreText.text = $"{defenseState.score}";

            waveText.text = $"Wave {defenseState.nextWave}";
            waveFill.fillAmount = defenseState.nextWave * 1.0f / defenseState.waveStates.Count;
            energyText.text = $"{defenseState.energy}/{DefenseState.ENERGY_SHOT_MAX_CHARGE}";

            if (defenseState.energy >= DefenseState.ENERGY_SHOT_MAX_CHARGE)
            {
                skillButton.interactable = true;
            }
            else
            {
                isSkillActivating = false;
                skillActivatingGO.SetActive(isSkillActivating);
                skillButton.interactable = false;
            }

            var toRemoveEnemies = enemies.Where(x => !defenseState.enemyStates.ContainsKey(x.Key)).Select(x => x.Key).ToList();
            foreach (var id in toRemoveEnemies)
            {
                enemies[id].gameObject.SetActive(false);
                enemies.Remove(id);
            }

            foreach (var p in defenseState.enemyStates)
            {
                var enemy = p.Value;
                EnemyObject enemyObject;
                if (!enemies.TryGetValue(enemy.id, out enemyObject))
                {
                    enemyObject = NewGameObject("EnemyObject").GetComponent<EnemyObject>();
                    enemyObject.transform.localPosition = new Vector3(enemy.pos.x, enemy.pos.y, 0);
                    enemyObject.SetType(enemy.enemyType);
                    enemies.Add(enemy.id, enemyObject);
                }
                enemyObject.transform.localPosition = new Vector3(enemy.pos.x, enemy.pos.y, 0);
                enemyObject.hpText.text = $"{enemy.hp}";
            }

            //Bullet
            var toRemoveBullets = bullets.Where(x => !defenseState.bulletStates.ContainsKey(x.Key)).Select(x => x.Key).ToList();
            foreach (var id in toRemoveBullets)
            {
                bullets[id].gameObject.SetActive(false);
                bullets.Remove(id);
            }

            foreach (var p in defenseState.bulletStates)
            {
                var bullet = p.Value;
                BulletObject bulletObject;
                if (!bullets.TryGetValue(bullet.id, out bulletObject))
                {
                    bulletObject = NewGameObject("BulletObject").GetComponent<BulletObject>();
                    bullets.Add(bullet.id, bulletObject);
                }
                bulletObject.transform.localPosition = new Vector3(bullet.pos.x, bullet.pos.y, 0);
            }
        }
    }
}
