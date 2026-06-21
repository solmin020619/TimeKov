using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 와이번 보스 불 VFX 전부 새로 생성(URP 네이티브). Vefects 팩(URP 비호환=마젠타/흰박스)을 완전히 대체.
/// 만드는 것:
///   - 자체 생성 텍스처(부드러운 라디얼 글로우 / 연기 구름)  ->  Assets/12.VFX/WyvernFire/
///   - URP Particles/Unlit 머티리얼(additive 불 / alpha 연기)  - 검증된 블렌드 셋업
///   - 파이어볼 발사체 비주얼 / 폭발 / 화염방사 프리팹(파티클 시스템 조립)
/// 그리고 기존 와이번 프리팹에 바로 재배선:
///   - Wyvern_Fireball.prefab 의 비주얼 자식 + explodeVfx 교체
///   - 보스 분출/다이브/텔레그래프 VFX(eruptionVfx/slamVfx/telegraphVfx)는 Wire Eric VFX 메뉴에서 배선
/// 멱등(다시 돌리면 덮어쓰기). 메뉴: Tools > Enemy > Build Wyvern Fire VFX (clean URP)
/// </summary>
public static class WyvernVfxBuilder
{
    const string Folder        = "Assets/12.VFX/WyvernFire";
    const string GlowTexPath   = Folder + "/T_WyvernGlow.png";
    const string SmokeTexPath  = Folder + "/T_WyvernSmoke.png";
    const string AddMatPath    = Folder + "/M_WyvernFire_Add.mat";
    const string SmokeMatPath  = Folder + "/M_WyvernSmoke.mat";
    const string FireballVfx   = Folder + "/Wyvern_VFX_Fireball.prefab";
    const string ExplosionVfx  = Folder + "/Wyvern_VFX_Explosion.prefab";
    const string SpreadFireVfx = Folder + "/Wyvern_VFX_SpreadFire.prefab";

    const string FireballPrefab = "Assets/05.Prefabs/Enemy/Wyvern_Fireball.prefab";
    const string BossPrefab      = "Assets/05.Prefabs/Enemy/Enemy_Wyvern_Boss.prefab";

    const string UrpParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";

    // Eric VFX Studio 팩(URP 네이티브 셰이더). 데모 기준 매칭: FX_Fireball=파이어볼 / FX_LightPillar=분출(브레스 대체) / FX_Weapon Effect=내려찍기 강타.
    const string EricFolder   = "Assets/Eric VFX Studio/Free Game VFX/Prefab";
    const string EricFireball = EricFolder + "/FX_Fireball.prefab";
    const string EricPillar   = EricFolder + "/FX_LightPillar.prefab";
    const string EricWeapon   = EricFolder + "/FX_Weapon Effect.prefab";
    const string EruptionWrap = Folder + "/Wyvern_Eruption.prefab";    // 자동소멸 래퍼
    const string SlamWrap     = Folder + "/Wyvern_Slam.prefab";        // 자동소멸 래퍼

    // 범위 텔레그래프(분출/다이브 발동 전 지면 링) - 자체 제작
    const string RingTexPath   = Folder + "/T_WyvernRing.png";
    const string RingMatPath   = Folder + "/M_WyvernRing.mat";
    const string TelegraphPath = Folder + "/Wyvern_Telegraph.prefab";

    [MenuItem("Tools/Enemy/Build Wyvern Fire VFX (clean URP)")]
    public static void Build()
    {
        var shader = Shader.Find(UrpParticlesUnlit);
        if (shader == null)
        {
            EditorUtility.DisplayDialog("VFX 생성 실패", $"URP 셰이더 못 찾음:\n{UrpParticlesUnlit}", "확인");
            return;
        }
        if (!EditorUtility.DisplayDialog("와이번 불 VFX 생성",
            "깔끔한 URP 불 VFX를 새로 만들고 와이번 프리팹에 바로 연결한다:\n" +
            "  - 글로우/연기 텍스처 + URP additive 머티리얼\n" +
            "  - 파이어볼 / 폭발 / 화염방사 프리팹\n" +
            "  - Wyvern_Fireball / Enemy_Wyvern_Boss 재배선\n\n" +
            "Vefects 팩은 더 안 씀. 계속?", "생성", "취소")) return;

        EnsureFolder(Folder);

        var glowTex  = MakeGlowTexture(GlowTexPath, 128);
        var smokeTex = MakeSmokeTexture(SmokeTexPath, 128);

        var addMat   = MakeMaterial(AddMatPath, shader, glowTex, true);
        var smokeMat = MakeMaterial(SmokeMatPath, shader, smokeTex, false);

        var fireball  = BuildFireball(addMat);
        var explosion = BuildExplosion(addMat, smokeMat);
        BuildSpreadFire(addMat);   // (구) 화염방사 비주얼 - 현재 미사용이나 폴백 자산으로 생성만 유지

        RewireFireballPrefab(fireball, explosion);
        RewireBossPrefab(null, null, null);   // 코드 폴백엔 분출/강타/텔레그래프 비주얼 없음(공격은 동작). 비주얼은 Wire Eric VFX 메뉴.

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[WyvernVFX] 생성 완료(URP 네이티브, Vefects 미사용).\n" +
            $"  파이어볼 비주얼: {FireballVfx}\n  폭발: {ExplosionVfx}\n" +
            $"  재배선: {FireballPrefab} (자식+explodeVfx)\n\n" +
            "다음(종욱):\n" +
            "1. Play -> 와이번 입벌릴 때 불덩이, 착탄 시 폭발 확인. (분출/강타 비주얼은 Wire Eric VFX 메뉴)\n" +
            "2. 크기/색 조정은 프리팹 안 파티클의 Start Size / Color over Lifetime 인스펙터.\n" +
            "3. 발사 위치 안 맞으면 WyvernBossController.fireOffset(입 근처).");
    }

    // ===== Eric VFX 팩 연결 (다운받은 URP 네이티브 불 VFX를 와이번에 배선) =====
    // FX_Fireball=파이어볼(자식) / FX_LightPillar=분출(브레스 대체) / FX_Weapon Effect=내려찍기 강타.
    // 분출/강타 프리팹은 stopAction=None이라 자동소멸 래퍼로 감싼다. 파이어볼은 발사체와 함께 소멸하니 직접 중첩.
    // 파이어볼 착탄 폭발은 코드 제작 버스트 유지(에어버스트엔 지면 데칼 없는 게 나음).
    [MenuItem("Tools/Enemy/Wire Eric VFX Into Wyvern")]
    public static void WireEricVfx()
    {
        var fxFireball = AssetDatabase.LoadAssetAtPath<GameObject>(EricFireball);
        var fxPillar   = AssetDatabase.LoadAssetAtPath<GameObject>(EricPillar);
        var fxWeapon   = AssetDatabase.LoadAssetAtPath<GameObject>(EricWeapon);
        if (fxFireball == null || fxPillar == null || fxWeapon == null)
        {
            EditorUtility.DisplayDialog("Eric VFX 연결 실패",
                $"프리팹을 못 찾음(FX_Fireball/FX_LightPillar/FX_Weapon Effect). 경로 확인:\n{EricFolder}", "확인");
            return;
        }
        if (!EditorUtility.DisplayDialog("Eric VFX -> 와이번 연결",
            "Eric VFX(URP 네이티브)를 와이번에 연결한다:\n" +
            "  - 파이어볼 = FX_Fireball\n" +
            "  - 분출(브레스 대체) = FX_LightPillar (자동소멸 래퍼)\n" +
            "  - 내려찍기 강타 = FX_Weapon Effect (자동소멸 래퍼)\n" +
            "  - 파이어볼 착탄 = 코드 버스트(Wyvern_VFX_Explosion) 유지\n\n계속?", "연결", "취소")) return;

        EnsureFolder(Folder);
        var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionVfx);
        var eruption  = BuildWrapper("Wyvern_Eruption", EruptionWrap, fxPillar, 2.5f, Vector3.zero);
        var slam      = BuildWrapper("Wyvern_Slam", SlamWrap, fxWeapon, 2.5f, Vector3.zero);

        GameObject telegraph = null;                    // 지면 범위 링(분출/다이브 전 표시)
        // 전용 셰이더(ZTest Always = 경사/지형 뒤여도 보임). 없으면 URP Particles/Unlit 폴백.
        AssetDatabase.Refresh();   // 새로 추가한 .shader가 아직 임포트 안 됐으면 임포트(첫 실행서 Shader.Find 성공 보장)
        var ringShader = Shader.Find("Wyvern/TelegraphOnTop");
        bool onTop = ringShader != null;
        if (ringShader == null) ringShader = Shader.Find(UrpParticlesUnlit);
        if (ringShader != null)
        {
            var ringTex = MakeRingTexture(RingTexPath, 128);
            Material ringMat;
            if (onTop)
            {
                ringMat = AssetDatabase.LoadAssetAtPath<Material>(RingMatPath);
                if (ringMat == null) { ringMat = new Material(ringShader); AssetDatabase.CreateAsset(ringMat, RingMatPath); }
                else ringMat.shader = ringShader;
                ringMat.SetTexture("_BaseMap", ringTex);
                if (ringMat.HasProperty("_BaseColor")) ringMat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(ringMat);
            }
            else ringMat = MakeMaterial(RingMatPath, ringShader, ringTex, true);
            telegraph = BuildTelegraph(ringMat);
        }

        RewireFireballPrefab(fxFireball, explosion);    // 자식=FX_Fireball, explodeVfx=코드 버스트
        RewireBossPrefab(eruption, slam, telegraph);    // eruptionVfx, slamVfx, telegraphVfx

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "[WyvernVFX] Eric VFX 연결 완료(URP 네이티브).\n" +
            "  파이어볼=FX_Fireball / 분출=FX_LightPillar / 다이브강타=FX_Weapon Effect / 범위링=자체제작 / 착탄=코드버스트.\n\n" +
            "다음(종욱):\n" +
            "1. Play -> 파이어볼(지형 충돌 폭발) / 분출(범위링 후 솟구침) / 공중 다이브 강타(범위링 후 내려찍기) 확인.\n" +
            "2. 분출 타이밍/범위는 컨트롤러 eruption*, 다이브는 dive*(높이/체공/낙하/반경) 값.\n" +
            "3. 래퍼 지속이 짧/길면 Wyvern_Eruption / Wyvern_Slam 루트 PS Duration.\n" +
            "4. 필러/강타/링 크기 안 맞으면 래퍼 자식 Scale 또는 컨트롤러 radius 값.");
    }

    // ===== 텍스처 생성 =====

    // 부드러운 라디얼 글로우: 중심 밝고 가장자리로 0. rgb/alpha 둘 다 falloff 구워서 어떤 블렌드든 가장자리 부드럽게.
    static Texture2D MakeGlowTexture(string path, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0=중심 1=가장자리
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                                          // 부드러운 falloff
                float core = Mathf.Clamp01(1f - d * 1.6f);         // 중심 코어 더 밝게
                core = core * core;
                float lum = Mathf.Clamp01(a * 0.7f + core * 0.6f);
                tex.SetPixel(x, y, new Color(lum, lum, lum, lum));
            }
        }
        tex.Apply();
        WritePng(tex, path);
        return ImportAsGlow(path);
    }

    // 연기 구름: 라디얼 falloff x 약한 퍼린 노이즈. alpha 블렌드용(rgb=흰색, 색은 파티클이 어둡게 틴트).
    static Texture2D MakeSmokeTexture(string path, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        float c = (size - 1) * 0.5f;
        float ns = 3.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float radial = Mathf.Clamp01(1f - d);
                radial = radial * radial;
                float n = Mathf.PerlinNoise(x / (float)size * ns, y / (float)size * ns);
                n = Mathf.Lerp(0.55f, 1f, n);
                float a = Mathf.Clamp01(radial * n);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        WritePng(tex, path);
        return ImportAsGlow(path);
    }

    static void WritePng(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    static Texture2D ImportAsGlow(string path)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Default;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.sRGBTexture = true;
            imp.maxTextureSize = 256;
            imp.textureCompression = TextureImporterCompression.Uncompressed;   // 작은 그라데이션=블록 아티팩트 회피
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ===== 머티리얼 (검증된 URP Particles/Unlit 셋업) =====

    static Material MakeMaterial(string path, Shader shader, Texture tex, bool additive)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = shader;

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_Surface", 1f);                         // Transparent
        mat.SetFloat("_Blend", additive ? 2f : 0f);           // 2=Additive 0=Alpha
        mat.SetFloat("_SrcBlend", additive ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);   // 틴트는 파티클 색이 담당
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ===== 프리팹 조립 =====

    // 파이어볼 발사체 비주얼(루프). Wyvern_Fireball 의 자식 -> 발사체와 함께 소멸(자동소멸 불필요).
    static GameObject BuildFireball(Material add)
    {
        var root = new GameObject("Wyvern_VFX_Fireball");

        // 코어: 작고 밝은 흰-노랑 덩이(로컬)
        var core = NewPS(root, "Core", add);
        Main(core, life: 0.35f, speed: 0f, size: 0.55f, world: false, gravity: 0f, loop: true, dur: 1f);
        SetEmissionRate(core, 35f);
        Shape(core, ParticleSystemShapeType.Sphere, 0.08f, 0f);
        ColorOverLife(core, CoreGradient());
        Size(core, Curve(0f, 1f, 0.5f, 1.1f, 1f, 0.7f));

        // 불꽃: 주황->빨강 트레일(월드 = 비행하며 꼬리)
        var flames = NewPS(root, "Flames", add);
        Main(flames, life: 0.45f, speed: 0.3f, size: 0.5f, world: true, gravity: -0.4f, loop: true, dur: 1f);
        SetEmissionRate(flames, 45f);
        Shape(flames, ParticleSystemShapeType.Sphere, 0.12f, 0f);
        ColorOverLife(flames, FireGradient());
        Size(flames, Curve(0f, 0.4f, 0.25f, 1f, 1f, 0.25f));

        // 스파크: 작은 불티(월드, 늘림)
        var sparks = NewPS(root, "Sparks", add);
        Main(sparks, life: 0.5f, speed: 1.2f, size: 0.07f, world: true, gravity: 0.6f, loop: true, dur: 1f);
        SetEmissionRate(sparks, 18f);
        Shape(sparks, ParticleSystemShapeType.Sphere, 0.15f, 0f);
        ColorOverLife(sparks, SparkGradient());
        Stretch(sparks, 2.2f);

        SavePrefab(root, FireballVfx);
        return AssetDatabase.LoadAssetAtPath<GameObject>(FireballVfx);
    }

    // 폭발(1회성, 루트 컨트롤러 PS stopAction=Destroy 로 자동소멸).
    static GameObject BuildExplosion(Material add, Material smoke)
    {
        var root = new GameObject("Wyvern_VFX_Explosion");
        AddAutoDestroyController(root, 1.8f);

        // 플래시: 한 방 큰 섬광
        var flash = NewPS(root, "Flash", add);
        Main(flash, life: 0.18f, speed: 0f, size: 3.2f, world: true, gravity: 0f, loop: false, dur: 0.2f);
        Burst(flash, 1);
        Shape(flash, ParticleSystemShapeType.Sphere, 0.01f, 0f);
        ColorOverLife(flash, CoreGradient());
        Size(flash, Curve(0f, 0.5f, 0.25f, 1f, 1f, 0.7f));

        // 화염 버스트: 사방으로
        var fire = NewPS(root, "FireBurst", add);
        Main(fire, life: 0.55f, speed: 5f, size: 0.7f, world: true, gravity: -0.5f, loop: false, dur: 0.4f);
        Burst(fire, 28);
        Shape(fire, ParticleSystemShapeType.Sphere, 0.2f, 0f);
        ColorOverLife(fire, FireGradient());
        Size(fire, Curve(0f, 0.35f, 0.3f, 1f, 1f, 0.5f));

        // 연기: 천천히 위로(알파)
        var sm = NewPS(root, "Smoke", smoke);
        Main(sm, life: 1.3f, speed: 1.6f, size: 1f, world: true, gravity: -0.6f, loop: false, dur: 0.5f);
        Burst(sm, 12);
        Shape(sm, ParticleSystemShapeType.Sphere, 0.3f, 0f);
        ColorOverLife(sm, SmokeGradient());
        Size(sm, Curve(0f, 0.4f, 1f, 1.4f));
        Rotation(sm, 90f);

        // 스파크: 사방 + 중력
        var sparks = NewPS(root, "Sparks", add);
        Main(sparks, life: 0.6f, speed: 6.5f, size: 0.08f, world: true, gravity: 3f, loop: false, dur: 0.2f);
        Burst(sparks, 32);
        Shape(sparks, ParticleSystemShapeType.Sphere, 0.15f, 0f);
        ColorOverLife(sparks, SparkGradient());
        Stretch(sparks, 2.5f);

        SavePrefab(root, ExplosionVfx);
        return AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionVfx);
    }

    // 화염방사(정면 콘, 1회성 자동소멸). 프리팹 +Z 가 분사 방향(보스가 transform.rotation 으로 스폰).
    static GameObject BuildSpreadFire(Material add)
    {
        var root = new GameObject("Wyvern_VFX_SpreadFire");
        AddAutoDestroyController(root, 1.2f);

        var cone = NewPS(root, "Cone", add);
        Main(cone, life: 0.5f, speed: 10f, size: 0.45f, world: true, gravity: -0.3f, loop: false, dur: 0.6f);
        SetEmissionRate(cone, 90f);
        ShapeCone(cone, 22f, 0.25f);
        ColorOverLife(cone, FireGradient());
        Size(cone, Curve(0f, 0.4f, 0.3f, 1f, 1f, 0.6f));

        var embers = NewPS(root, "Embers", add);
        Main(embers, life: 0.6f, speed: 11f, size: 0.07f, world: true, gravity: 1.2f, loop: false, dur: 0.6f);
        SetEmissionRate(embers, 30f);
        ShapeCone(embers, 26f, 0.2f);
        ColorOverLife(embers, SparkGradient());
        Stretch(embers, 2.2f);

        SavePrefab(root, SpreadFireVfx);
        return AssetDatabase.LoadAssetAtPath<GameObject>(SpreadFireVfx);
    }

    // ===== 파티클 헬퍼 =====

    static ParticleSystem NewPS(GameObject parent, string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.alignment = ParticleSystemRenderSpace.View;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingFudge = -5f;
        return ps;
    }

    static void Main(ParticleSystem ps, float life, float speed, float size, bool world, float gravity, bool loop, float dur)
    {
        var m = ps.main;
        m.startLifetime = life;
        m.startSpeed = speed;
        m.startSize = size;
        m.startColor = UnityEngine.Color.white;
        m.gravityModifier = gravity;
        m.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        m.loop = loop;
        m.duration = dur;
        m.playOnAwake = true;
        m.maxParticles = 1000;
    }

    static void SetEmissionRate(ParticleSystem ps, float rate)
    {
        var e = ps.emission; e.enabled = true; e.rateOverTime = rate;
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var e = ps.emission; e.enabled = true; e.rateOverTime = 0f;
        e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius, float angle)
    {
        var s = ps.shape; s.enabled = true; s.shapeType = type; s.radius = radius; s.angle = angle;
    }

    static void ShapeCone(ParticleSystem ps, float angle, float radius)
    {
        // Cone 셰이프 기본 방출축 = 로컬 +Z(=프리팹 forward). 보스가 transform.rotation 으로 스폰하니 정면 분사.
        var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Cone;
        s.angle = angle; s.radius = radius; s.rotation = Vector3.zero;
    }

    static void ColorOverLife(ParticleSystem ps, Gradient g)
    {
        var c = ps.colorOverLifetime; c.enabled = true; c.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void Size(ParticleSystem ps, AnimationCurve curve)
    {
        var s = ps.sizeOverLifetime; s.enabled = true; s.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    static void Rotation(ParticleSystem ps, float degPerSec)
    {
        var r = ps.rotationOverLifetime; r.enabled = true;
        r.z = new ParticleSystem.MinMaxCurve(degPerSec * Mathf.Deg2Rad);
    }

    static void Stretch(ParticleSystem ps, float lengthScale)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.08f;
        r.lengthScale = lengthScale;
    }

    // 루트에 빈 컨트롤러 PS(아무것도 안 뿜음) + duration 후 stopAction=Destroy = 새 스크립트 없이 자동소멸.
    static void AddAutoDestroyController(GameObject root, float seconds)
    {
        var ps = root.AddComponent<ParticleSystem>();
        var m = ps.main;
        m.loop = false; m.duration = seconds; m.startLifetime = 0.01f;
        m.playOnAwake = true; m.stopAction = ParticleSystemStopAction.Destroy;
        var e = ps.emission; e.enabled = false;
        var r = root.GetComponent<ParticleSystemRenderer>(); r.enabled = false;
    }

    static AnimationCurve Curve(params float[] tv)
    {
        var c = new AnimationCurve();
        for (int i = 0; i + 1 < tv.Length; i += 2) c.AddKey(tv[i], tv[i + 1]);
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    // ===== 색 그라데이션 =====

    static Gradient Grad(Color[] cols, float[] colT, float[] aV, float[] aT)
    {
        var g = new Gradient();
        var ck = new GradientColorKey[cols.Length];
        for (int i = 0; i < cols.Length; i++) ck[i] = new GradientColorKey(cols[i], colT[i]);
        var ak = new GradientAlphaKey[aV.Length];
        for (int i = 0; i < aV.Length; i++) ak[i] = new GradientAlphaKey(aV[i], aT[i]);
        g.SetKeys(ck, ak);
        return g;
    }

    static Gradient CoreGradient() => Grad(
        new[] { new Color(1f, 0.97f, 0.8f), new Color(1f, 0.85f, 0.45f), new Color(1f, 0.55f, 0.2f) },
        new[] { 0f, 0.5f, 1f },
        new[] { 0f, 1f, 1f, 0f }, new[] { 0f, 0.1f, 0.6f, 1f });

    static Gradient FireGradient() => Grad(
        new[] { new Color(1f, 0.95f, 0.6f), new Color(1f, 0.65f, 0.2f), new Color(1f, 0.35f, 0.08f), new Color(0.55f, 0.1f, 0.03f) },
        new[] { 0f, 0.3f, 0.6f, 1f },
        new[] { 0f, 1f, 0.9f, 0f }, new[] { 0f, 0.1f, 0.55f, 1f });

    static Gradient SparkGradient() => Grad(
        new[] { new Color(1f, 0.9f, 0.5f), new Color(1f, 0.5f, 0.15f), new Color(0.6f, 0.12f, 0.04f) },
        new[] { 0f, 0.5f, 1f },
        new[] { 1f, 1f, 0f }, new[] { 0f, 0.6f, 1f });

    static Gradient SmokeGradient() => Grad(
        new[] { new Color(0.12f, 0.1f, 0.09f), new Color(0.07f, 0.06f, 0.06f) },
        new[] { 0f, 1f },
        new[] { 0f, 0.5f, 0.45f, 0f }, new[] { 0f, 0.2f, 0.7f, 1f });

    // ===== 저장 / 재배선 =====

    static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    // Wyvern_Fireball.prefab: 옛 비주얼 자식 제거 -> 새 파이어볼 비주얼 자식 추가 + explodeVfx 세팅.
    static void RewireFireballPrefab(GameObject fireballVfx, GameObject explosionVfx)
    {
        var root = PrefabUtility.LoadPrefabContents(FireballPrefab);
        if (root == null) { Debug.LogWarning($"[WyvernVFX] 못 찾음: {FireballPrefab}"); return; }

        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        var vis = (GameObject)PrefabUtility.InstantiatePrefab(fireballVfx, root.scene);   // 임시 prefab 씬으로
        vis.transform.SetParent(root.transform, false);
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localRotation = Quaternion.identity;

        var fb = root.GetComponent<WyvernFireball>();
        if (fb != null)
        {
            var so = new SerializedObject(fb);
            var p = so.FindProperty("explodeVfx");
            if (p != null) p.objectReferenceValue = explosionVfx;
            so.ApplyModifiedProperties();
        }

        PrefabUtility.SaveAsPrefabAsset(root, FireballPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Enemy_Wyvern_Boss.prefab: WyvernBossController 분출/강타 VFX 세팅(null 가능).
    static void RewireBossPrefab(GameObject eruptionVfx, GameObject slamVfx, GameObject telegraphVfx)
    {
        var root = PrefabUtility.LoadPrefabContents(BossPrefab);
        if (root == null) { Debug.LogWarning($"[WyvernVFX] 못 찾음: {BossPrefab}"); return; }

        var ctrl = root.GetComponentInChildren<WyvernBossController>(true);
        if (ctrl != null)
        {
            var so = new SerializedObject(ctrl);
            var pe = so.FindProperty("eruptionVfx");  if (pe != null) pe.objectReferenceValue = eruptionVfx;
            var ps = so.FindProperty("slamVfx");      if (ps != null) ps.objectReferenceValue = slamVfx;
            var pt = so.FindProperty("telegraphVfx"); if (pt != null) pt.objectReferenceValue = telegraphVfx;
            so.ApplyModifiedProperties();
        }
        else Debug.LogWarning("[WyvernVFX] 보스 프리팹에 WyvernBossController 없음(VFX 미연결)");

        PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // 자동소멸 래퍼: 루트(빈 컨트롤러 PS, duration 후 stopAction=Destroy) + 외부 VFX 프리팹 자식.
    static GameObject BuildWrapper(string name, string path, GameObject vfxPrefab, float seconds, Vector3 childEuler)
    {
        var root = new GameObject(name);
        AddAutoDestroyController(root, seconds);
        var child = (GameObject)PrefabUtility.InstantiatePrefab(vfxPrefab);
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.Euler(childEuler);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // 지면 범위 텔레그래프 링: HorizontalBillboard(항상 지면에 평평) + scalingMode=Hierarchy(transform.scale 로 지름 제어) + 펄스 깜빡임.
    static GameObject BuildTelegraph(Material ringMat)
    {
        var root = new GameObject("Wyvern_Telegraph");
        var ps = NewPS(root, "Ring", ringMat);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        var m = ps.main;
        m.startLifetime = 0.5f; m.startSpeed = 0f; m.startSize = 1f;
        m.startColor = new Color(1f, 0.4f, 0.15f, 1f);            // 주황-적 경고색
        m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.scalingMode = ParticleSystemScalingMode.Hierarchy;       // transform.scale = 링 지름
        m.loop = true; m.duration = 1f; m.playOnAwake = true; m.maxParticles = 30;
        SetEmissionRate(ps, 5f);
        Shape(ps, ParticleSystemShapeType.Sphere, 0.001f, 0f);
        ColorOverLife(ps, Grad(
            new[] { new Color(1f, 1f, 1f), new Color(1f, 1f, 1f) }, new[] { 0f, 1f },
            new[] { 0f, 0.85f, 0f }, new[] { 0f, 0.4f, 1f }));     // 페이드 인/아웃 펄스
        SavePrefab(root, TelegraphPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath);
    }

    // 범위 링 텍스처: 가장자리 밝은 링 + 내부 옅은 경고 채움.
    static Texture2D MakeRingTexture(string path, int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        float c = (size - 1) * 0.5f;
        float r0 = 0.80f, w = 0.10f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Exp(-((d - r0) * (d - r0)) / (w * w));
                float fill = d < r0 ? 0.12f * (1f - d / r0) : 0f;
                float a = (d > 1f) ? 0f : Mathf.Clamp01(ring + fill);
                tex.SetPixel(x, y, new Color(a, a, a, a));   // rgb/alpha 둘 다 링 모양(전용셰이더=alpha 마스크 / 폴백 additive=rgb 모양 - 양쪽 대응)
            }
        }
        tex.Apply();
        WritePng(tex, path);
        return ImportAsGlow(path);
    }

    static void EnsureFolder(string fullPath)
    {
        if (AssetDatabase.IsValidFolder(fullPath)) return;
        var parts = fullPath.Split('/');
        string curr = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{curr}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(curr, parts[i]);
            curr = next;
        }
    }
}
