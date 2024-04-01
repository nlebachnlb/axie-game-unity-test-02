using System.Linq;
using Spine.Unity;
using UnityEngine;

public class EnemyObject : MonoBehaviour
{
    [SerializeField] GameObject[] _models;
    public TMPro.TextMeshPro hpText;
    MeshRenderer _activeMR;

    private void Start()
    {
        var models = gameObject.GetComponentsInChildren<SkeletonAnimation>(true).ToList();
        models.ForEach(x => { var slot = x.skeleton.FindSlot("shadow"); if (slot != null) slot.Attachment = null; });
    }

    public void SetType(int type)
    {
        for (int i = 0; i < _models.Length; i++)
        {
            _models[i].SetActive(i == type);
            if(i == type)
            {
                _activeMR = _models[i].GetComponent<MeshRenderer>();
            }
        }
    }

    private void Update()
    {
        if(_activeMR != null)
        {
            _activeMR.sortingOrder = (int)(-transform.localPosition.y * 1000 + transform.localPosition.x);
        }
    }
}
