using UnityEngine;

namespace AzureDreamsReimagined
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(0)]
    public sealed class PoolSwimmerView : MonoBehaviour
    {
        private TownInteriorScene room;
        private RetailTownResidentSpriteView residentView;

        internal void Configure(TownInteriorScene room)
        {
            this.room = room;
            residentView = GetComponent<RetailTownResidentSpriteView>();
        }

        private void Start()
        {
            if (!residentView) residentView = GetComponent<RetailTownResidentSpriteView>();
        }

        private void LateUpdate()
        {
            if (!room || !residentView)
                return;

            bool inside = room.TryGetPoolWaterHeight(transform.position, out float waterHeight);
            residentView.PresentPoolSwimming(inside, waterHeight);
        }

        private void OnDisable()
        {
            if (residentView != null)
                residentView.PresentPoolSwimming(false, 0f);
        }
    }
}
