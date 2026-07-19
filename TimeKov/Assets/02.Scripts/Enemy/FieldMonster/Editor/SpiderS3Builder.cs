// 거미S3(쫄몹: 자연/사막). 공용 FieldMonsterBuilder 에 설정만 넘긴다.
// 성격: 기민한 치고빠지기 — 발견→포효→접근→전조공격→회복→후퇴(길게 랜덤)→재공격.
// 실행: Window > Field Monster Builder 창의 버튼(툴 메뉴에는 노출 안 함).
public static class SpiderS3Builder
{
    const string Src = "Assets/00.창동에셋/몬스터/거미S3";

    public static void Build()
    {
        FieldMonsterBuilder.Build(new FieldMonsterBuildConfig
        {
            // 경로
            srcRoot         = Src,
            modelPrefabName = "거미S3_Skin1.prefab",
            skinMatName     = "M_Spider_S3_Skin1.mat",
            skinTexPath     = Src + "/Textures/Skin1/T_Spider_S3_Skin1_AlbedoTransparency.tga",
            ctrlName        = "거미S3_Enemy.controller",   // -> 04.Animations/AnimationController/Enemy (기본)
            soPath          = "Assets/06.ScriptableObjects/Enemy/FieldData_SpiderS3.asset",
            prefabPath      = "Assets/05.Prefabs/Enemy/Enemy_SpiderS3.prefab",

            // 클립
            clipIdle  = "Anim@Spider_S1_idle1",
            clipFront = "Anim@Spider_S1_walk6",
            clipBack  = "Anim@Spider_S1_walk3",
            clipLeft  = "Anim@Spider_S1_straif_L",
            clipRight = "Anim@Spider_S1_straif_R",
            clipAttack = "Anim@Spider_S1_attack2",   // 0.75배속
            clipRoar   = "Anim@Spider_S1_attack1",   // 0.6배속 포효
            clipHit    = "Anim@Spider_S1_get_hit1",
            clipDeath  = "Anim@Spider_S1_death1",

            // 정체
            enemyName = "거미", enemyId = "spider_s3", sourceId = "MeleeBot_SpiderS3",

            // 스탯(기민·저체력) — 대부분 기본값과 동일
            maxHP = 55f, moveSpeed = 5f, attackDamage = 12f, attackRange = 2.5f,
            visionRange = 18f, visionAngle = 300f,

            // 사운드(있는 것만)
            spawnVfx  = "Assets/18.외부에셋/Eric VFX Studio/몬스터스폰VFX/Prefabs/FX_MagicCircle_Icearrow01.prefab",
            sndDetect = "Assets/10. Sound/1. 사운드 모음/몬스터/거미/M_SpiderFind.mp3",
            sndAttack = "Assets/10. Sound/1. 사운드 모음/몬스터/거미/M_SpiderAttack.mp3",
            sndDie    = "Assets/10. Sound/1. 사운드 모음/몬스터/거미/M_SpiderDie.mp3",
        });
    }
}
