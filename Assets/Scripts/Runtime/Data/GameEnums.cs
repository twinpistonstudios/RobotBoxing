namespace IronKnuckle.Data
{
    public enum PartSlot
    {
        Head = 0,
        Torso = 1,
        LeftArm = 2,
        RightArm = 3,
        Legs = 4,
        Core = 5
    }

    public enum PartRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }

    public enum RobotBrand
    {
        Scrapworks = 0,
        VoltTech = 1,
        TitanForge = 2,
        MantisLabs = 3,
        PyroCore = 4,
        ApexCorp = 5
    }

    public enum SurfaceFinish
    {
        PaintedMatte = 0,
        BrushedChrome = 1,
        CarbonFiber = 2,
        HazardIndustrial = 3,
        NeonCircuit = 4
    }

    public enum PunchType
    {
        LeftJab = 0,
        RightCross = 1,
        LeftHook = 2,
        RightHook = 3,
        Uppercut = 4,
        Special1 = 5,
        Special2 = 6
    }

    public enum FighterState
    {
        Idle = 0,
        Windup = 1,
        StrikeActive = 2,
        Recovery = 3,
        Guarding = 4,
        SlipLeft = 5,
        SlipRight = 6,
        HitStun = 7,
        GuardBroken = 8,
        Stunned = 9,
        KnockedDown = 10,
        KO = 11,
        Victory = 12
    }

    public enum AIStyle
    {
        Swarmer = 0,
        OutBoxer = 1,
        Slugger = 2,
        CounterPuncher = 3
    }

    public enum VisorExpression
    {
        Neutral = 0,
        Angry = 1,
        Hit = 2,
        Stunned = 3,
        KO = 4,
        Victory = 5
    }
}
