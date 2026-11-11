namespace AzureDreamsReimagined
{
    public static class TowerFloorRules
    {
        internal const int BeldoFloor = 40;
        public static bool AllowsRandomMonsters(int floor) => floor > 0 && floor != 2 && floor != BeldoFloor;
    }
}
