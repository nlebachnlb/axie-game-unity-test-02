using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Game
{
    [System.Serializable]
    public class InventorySlot
    {
        public string key;
        public GameObject frame;
        public TMPro.TextMeshProUGUI amoutText;
    }

    public class UILayer : MonoBehaviour
    {
        [SerializeField] GameObject frameResult;
        [SerializeField] TMPro.TextMeshProUGUI resultText;

        public void SetResultFrame(bool b)
        {
            if (this.frameResult != null)
            {
                this.frameResult.SetActive(b);
            }
        }

        public void SetResultText(bool isWon)
        {
            if (this.resultText != null)
            {
                if (isWon)
                {
                    this.resultText.text = "YOU WIN!!!";
                }
                else
                {
                    this.resultText.text = "YOU LOSE!!!";
                }
            }
        }
    }
}
