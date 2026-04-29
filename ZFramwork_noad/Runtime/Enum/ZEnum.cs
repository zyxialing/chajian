
public enum ConnectionPointType { In, Out }

public enum FixedPointType
{
    self_far_point = 0,
    enemy_far_point,
    enemy_point
}

public enum NodeType
{
    root = 0,
    compose,
    idle,
    finding,
    attack,
    condition,
    rest,
    change,
    search
}
public enum CondType//条件类型枚举
{
    None = 0,
    canBeAttack__dis,
    _f_canFarBeAttack__dis,
    _m_keepTime__time,
    _t_patrolTimes__times,
    haveNoEnemy__enemy,
    haveEnemy__enemy,
    _d_randKeepTime__time,
    _h_lowHp__hp,
    _h_highHp__hp,
    haveSkyEnemy__enemy,
    haveNoSkyEnemy__enemy,
    haveGroundEnemy__enemy,
    haveNoGroundEnemy__enemy,
    _m_outCdTime__time,
    _m_intCdTime__time,
    skyCanBeAttack__dis,
    isAttacking__attack,
    isNotAttacking__attack,
    _m_outGlobalCdTime__time,
    _m_intGlobalCdTime__time,
    _m_birthOver__time,
    _m_birthNotOver__time,
    _o_isLeader__other,
    _o_isNotLeader__other,
    _m_disLeader__dis,
    haveTargetEnemy__enemy,
    canNotBeAttack__dis,
    haveNoTargetEnemy__enemy,
    _f_canNotFarBeAttack__dis,
    haveOtherFriend__enemy,
    haveNoOtherFriend__enemy,
    _h_enemyLowHp__hp,
    _h_enemyHeighHp__hp,
    _m_keepLifeTime__time,
    IsSelf__enemy,
    IsNotSelf__enemy,
    findStop__other,
}
public enum MapPointType
{
    city = 0,
    battle
}
public enum BuffType
{
    None = 0,
    Frozen = 1,//冰冻效果
    Bleeding = 2,//流血
    Burning = 3,//燃烧
    Light = 4,//光耀
    PoorLess = 5,//虚弱
    Toxin = 6,//中毒
    Shadow = 7,//暗影


    Dizziness = 101,//眩晕
    VoidAvoidance = 102,//虚空闪避
    DamageReduction = 103,//伤害减免
    DamageShift = 104,//伤害转移给队友
    DamageBack = 105,//伤害反弹给敌人
    DamageAdd = 106,//伤害增加
    HealHp = 107,//治疗buff


    FrozenImmunity = 1001,//冰冻效果免疫
    BleedingImmunity = 1002,//流血效果免疫
    BurningImmunity = 1003,//燃烧效果免疫
    LightImmunity = 1004,//光耀效果免疫
    PoorLessImmunity = 1005,//虚弱效果免疫
    ToxinImmunity = 1006,//中毒效果免疫
    ShadowImmunity = 1007,//暗影效果免疫

    DizzinessImmunity = 10001,//眩晕效果免疫
}

public enum HurtType
{
    BUFF,
    SKILL
}

public enum SortType
{
    Lv = 0,
    Star,
    Quality,
    Job
}
public enum PropSortType
{
    Quality,
}

public enum QualityType
{
    White = 0,
    Green,
    Blue,
    Purple,
    Golden
}

public enum JobType
{
    None = 0,
    Framer,
    Job1,
    Job2,
    Job3,
    Job4,
    Job5,
    Job6,
    Job7,
    Monster
}

public enum Emoji
{
    None = 0,
    Anger_EmojiAngry = 1,
    Anger_EmojiAngry2 = 2,
    Anger_EmojiMad,
    Anger_EmojiSinister,
    Disgust_EmojiNauseous,
    Disgust_EmojiPuke,
    Disgust_EmojiQueasy,
    Disgust_EmojiSick,
    Joy_EmojiCool,
    Joy_EmojiCute,
    Joy_EmojiDerp,
    Joy_EmojiDerpGasp,
    Joy_EmojiDrool,
    Joy_EmojiEvilLaugh,
    Joy_EmojiHappy,
    Joy_EmojiKiss,
    Joy_EmojiKissyface,
    Joy_EmojiLaughCry,
    Joy_EmojiLaughSweatdrop,
    Joy_EmojiOwO,
    Joy_EmojiOwOEyebrow,
    Joy_EmojiSilly,
    Joy_EmojiSillyHappy,
    Joy_EmojiSillySmile,
    Joy_EmojiSillyWink,
    Joy_EmojiSmile,
    Joy_EmojiStarstruck,
    Joy_EmojiUwU,
    Joy_EmojiXD,
    Misc_EmojiHeart,
    Misc_EmojiPoop,
    Misc_EmojiThumbsDown,
    Misc_EmojiThumbsUp,
    Sadness_EmojiCry,
    Sadness_EmojiDisappointed,
    Sadness_EmojiExpressionless,
    Sadness_EmojiPleading,
    Sadness_EmojiSad,
    Sadness_EmojiSadCry,
    Sadness_EmojiTearyEyes,
    Surprise_EmojiBlush,
    Surprise_EmojiClenchTeeth,
    Surprise_EmojiCrazy,
    Surprise_EmojiNervous,
    Surprise_EmojiScared,
    Surprise_EmojiShocked,
    Tired_EmojiDead,
    Tired_EmojiDeadTired,
    Tired_EmojiInjured,
    Tired_EmojiSleep,
    Tired_EmojiYawn
}

public enum RaceType
{
    Human = 0,
    Spirit,
    Animal,
    Dragon,
}
public enum WeaponType
{
    None = 0,

    Axe = 1,
    Bow = 2,
    Dagger = 3,
    Shield = 4,
    Staff = 5,
    Stick = 6,
    Sword = 7,

    Axe_Axe = 11,
    Axe_Bow = 12,
    Axe_Dagger = 13,
    Axe_Shield = 14,
    Axe_Staff = 15,
    Axe_Stick = 16,
    Axe_Sword = 17,

    Bow_Axe = 21,
    Bow_Bow = 22,
    Bow_Dagger = 23,
    Bow_Shield = 24,
    Bow_Staff = 25,
    Bow_Stick = 26,
    Bow_Sword = 27,

    Dagger_Axe = 31,
    Dagger_Bow = 32,
    Dagger_Dagger = 33,
    Dagger_Shield = 34,
    Dagger_Staff = 35,
    Dagger_Stick = 36,
    Dagger_Sword = 37,

    Shield_Axe = 41,
    Shield_Bow = 42,
    Shield_Dagger = 43,
    Shield_Shield = 44,
    Shield_Staff = 45,
    Shield_Stick = 46,
    Shield_Sword = 47,

    Staff_Axe = 51,
    Staff_Bow = 52,
    Staff_Dagger = 53,
    Staff_Shield = 54,
    Staff_Staff = 55,
    Staff_Stick = 56,
    Staff_Sword = 57,

    Stick_Axe = 61,
    Stick_Bow = 62,
    Stick_Dagger = 63,
    Stick_Shield = 64,
    Stick_Staff = 65,
    Stick_Stick = 66,
    Stick_Sword = 67,

    Swrod_Axe = 71,
    Swrod_Bow = 72,
    Swrod_Dagger = 73,
    Swrod_Shield = 74,
    Swrod_Staff = 75,
    Swrod_Stick = 76,
    Swrod_Sword = 77,
}

public enum TeamType
{
    Character = 0,
    Pet,
}
public enum TeamPanelType
{
    Character = 0,
    Formation,
}
public enum ShopType
{
    Prop = 0,
    Equip,
    PropLimit,
    EquipLimit,
}
public enum BagType
{
    Prop = 0,
    Equip,
}
public enum PropType
{
    Food,
    Potion
}
public enum EquipType
{
    Weapon = 0,
    Clothing,
    Pants,
    Shoe
}
public enum UseType
{
    Direct,
    Food,

}

public enum BattleEndType
{
    None,
    KillAll
}
public enum DistributionType
{
    Scatter,
    PoissonFast,
    Cluster
}