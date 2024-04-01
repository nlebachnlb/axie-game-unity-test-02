using Spine.Unity;
using UnityEngine;

public class ImpactObject : MonoBehaviour
{
    [SerializeField] SkeletonAnimation spine;

    public void Show()
    {
        var track = spine.state.SetAnimation(0, "animation", false);
        track.Complete += (_) =>
        {
            gameObject.SetActive(false);
        };
    }
}
