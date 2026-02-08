using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("Weapon Equip Visual")]
    [Tooltip("���� �������� ���� ����(1��Ī ViewModel�̸� ik_hand_gun ���� ��)")]
    public Transform weaponSocket;

    [Tooltip("���� ������ 5�� (index�� �Ʒ� itemId ���� ����)")]
    public GameObject[] weaponPrefabs;
    // index 0: 1101 SR
    // index 1: 1201 AK
    // index 2: 1202 SMG(MP7)
    // index 3: 1301 Shotgun
    // index 4: 1401 Pistol

    [System.Serializable]
    public struct WeaponOffset
    {
        public int itemId;
        public Vector3 localPos;
        public Vector3 localEuler;
        public Vector3 localScale;
    }

    [Header("Weapon Offsets (Per ItemId)")]
    [Tooltip("���� ���� ��ġ/ȸ�� ����. itemId���� 1���� �־�θ� �ڵ� �����.")]
    public WeaponOffset[] weaponOffsets;

    // ------------------ Animator (KINEMATION ����) ------------------
    [Header("Arms Animator (optional)")]
    [Tooltip("���� �ڵ� Ž��. (Gait �Ķ���� �ִ� Animator �켱)")]
    public Animator armsAnimator;

    [Tooltip("���� ���� �� ������ �⺻ ��Ʈ�ѷ�. ���� Awake���� �ڵ� ����")]
    public RuntimeAnimatorController defaultArmsController;

    [System.Serializable]
    public struct WeaponAnimLink
    {
        public int itemId;
        public WeaponAnimSettings animSettings;
    }

    [Header("Weapon Anim Settings (itemId -> SO)")]
    public WeaponAnimLink[] weaponAnimLinks;

    [Header("Layer Fix")]
    [Tooltip("KINEMATION ��Ʈ�ѷ��� �ȱ� ��鸲�� Additive/RightHand ���̾ �ִ� ��찡 ���Ƽ� weight�� ������ 1�� ����")]
    public bool forceLayerWeights = true;

    [Tooltip("FPSPlayer�� LateUpdate���� ���� ��� ���")]
    public bool forceLocomotionInLateUpdate = true;

    // ------------------ Fire ------------------
    [Header("Fire Point")]
    [Tooltip("����ĳ��Ʈ�� ���� ��� ���̾�(Enemy ��)")]
    public LayerMask hitMask;

    [Header("Bullet Visual")]
    [Tooltip("���� ���̴� ź ������(������ ���� �� ��)")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 40f;
    public float bulletLifeTime = 2f;

    [Header("Recoil")]
    private float recoilAccumYaw = 0f;
    public float recoilRecoverSpeed = 0f;

    public System.Action<float> onReloadStart;
    public System.Action onReloadEnd;

    private GameObject equippedWeaponGO;
    private Transform muzzle;
    private const string MUZZLE_NAME = "Muzzle";

    private int equippedItemId = 0;
    private ItemInfo weapon = null;

    private int currentAmmoInMag = 0;
    private bool isReloading = false;
    private float fireCooldown = 0f;
    private int recoilIndex = 0;
    private float lastFireTime = -999f;

    private PlayerController playerController;
    public CrosshairController crosshair;

    [Header("Debug")]
    public bool autoEquipOnStart = false;

    [Header("Input Mode")]
    public bool useExternalFireInput = true;

    private Camera cachedCam;

    // ------------------ Animator Hashes (KINEMATION �Ծ�) ------------------
    private static readonly int H_RELOAD_EMPTY = Animator.StringToHash("Reload_Empty");
    private static readonly int H_RELOAD_TAC = Animator.StringToHash("Reload_Tac");
    private static readonly int H_FIRE = Animator.StringToHash("Fire");
    private static readonly int H_FIREOUT = Animator.StringToHash("FireOut");
    private static readonly int H_EQUIP = Animator.StringToHash("Equip");
    private static readonly int H_EQUIP_OVR = Animator.StringToHash("Equip_Override");
    private static readonly int H_IDLE = Animator.StringToHash("Idle");
    private static readonly int H_UNEQUIP_TRG = Animator.StringToHash("UnEquip");

    private static readonly int H_GAIT = Animator.StringToHash("Gait");      // float
    private static readonly int H_TACSPRINT = Animator.StringToHash("TacSprint"); // float 0/1
    private static readonly int H_ISINAIR = Animator.StringToHash("IsInAir");    // bool

    // cached param existence
    private bool _hasGait, _hasTacSprint, _hasIsInAir;

    // cached locomotion
    private float _gait01;
    private bool _tacSprint;
    private bool _isInAir;

    // current anim settings
    private WeaponAnimSettings _currentAnimSettings;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        cachedCam = Camera.main;

        EnsureArmsAnimator();
        if (armsAnimator != null && defaultArmsController == null)
            defaultArmsController = armsAnimator.runtimeAnimatorController;
    }

    private void Start()
    {
        if (autoEquipOnStart)
            EquipByItemId(1201);
    }

    private void Update()
    {
        EnsureArmsAnimator();

        if (forceLayerWeights)
            ForceKinemationLayerWeights();

        UpdateLocomotionParams();

        if (crosshair != null)
        {
            crosshair.SetEnabled(weapon != null);
            crosshair.SetRunning(playerController != null && playerController.IsRunning);
        }

        if (weapon == null) return;

        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        if (isReloading) return;

        if (Input.GetKeyDown(KeyCode.R) && currentAmmoInMag < weapon.magazinesize)
            StartCoroutine(ReloadRoutine());

        if (useExternalFireInput) return;

        bool fireInput = weapon.isAutomatic == 1
            ? Input.GetMouseButton(0)
            : Input.GetMouseButtonDown(0);

        if (fireInput)
            TryFire();
    }

    private void LateUpdate()
    {
        if (!forceLocomotionInLateUpdate) return;
        ApplyLocomotionNow();
    }

    // ------------------ Animator: Detect + Cache ------------------
    private void EnsureArmsAnimator()
    {
        if (armsAnimator != null)
        {
            CacheParamExistence();
            return;
        }

        // �ڽ� ���� Animator �� Gait �Ķ���� �ִ� Animator �켱 Ž��
        var anims = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null || a.runtimeAnimatorController == null) continue;
            if (HasParam(a, "Gait"))
            {
                armsAnimator = a;
                break;
            }
        }

        // fallback
        if (armsAnimator == null && anims.Length > 0)
            armsAnimator = anims[0];

        CacheParamExistence();
    }

    private bool HasParam(Animator a, string name)
    {
        if (a == null) return false;
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == name) return true;
        return false;
    }

    private void CacheParamExistence()
    {
        if (armsAnimator == null) { _hasGait = _hasTacSprint = _hasIsInAir = false; return; }
        _hasGait = HasParam(armsAnimator, "Gait");
        _hasTacSprint = HasParam(armsAnimator, "TacSprint");
        _hasIsInAir = HasParam(armsAnimator, "IsInAir");
    }

    // ------------------ Layer Fix (�ȱ� ��鸲) ------------------
    private void ForceKinemationLayerWeights()
    {
        if (armsAnimator == null) return;

        void Set(string layerName, float w)
        {
            int idx = armsAnimator.GetLayerIndex(layerName);
            if (idx >= 0) armsAnimator.SetLayerWeight(idx, w);
        }

        Set("Additive", 1f);
        Set("RightHand", 1f);

        Set("Reload", 1f);
        Set("Grenade", 1f);
    }

    // ------------------ Locomotion Params ------------------
    private void UpdateLocomotionParams()
    {
        if (armsAnimator == null) return;

        float gait = 0f;

        // 1) MoveInput �켱
        if (playerController != null && playerController.MoveInput.sqrMagnitude > 0.0001f)
            gait = 1f;

        // 2) MoveInput�� 0�̸� Rigidbody �ӵ��� ����
        if (gait <= 0.001f)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 v = rb.linearVelocity; v.y = 0f;
                if (v.sqrMagnitude > 0.01f) gait = 1f;
            }
        }

        bool sprint = (playerController != null && playerController.IsRunning);
        bool inAir = false;

        _gait01 = gait;
        _tacSprint = sprint;
        _isInAir = inAir;

        ApplyLocomotionNow();
    }

    private void ApplyLocomotionNow()
    {
        if (armsAnimator == null) return;

        if (_hasGait) armsAnimator.SetFloat(H_GAIT, _gait01);
        if (_hasTacSprint) armsAnimator.SetFloat(H_TACSPRINT, _tacSprint ? 1f : 0f);
        if (_hasIsInAir) armsAnimator.SetBool(H_ISINAIR, _isInAir);
    }

    // ------------------ External Fire ------------------
    public void FireFromExternal()
    {
        if (weapon == null) return;

        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        if (isReloading) return;

        TryFire();
    }

    // ------------------ Equip / Unequip ------------------
    public bool EquipByItemId(int itemId)
    {
        if (itemId <= 0)
        {
            Unequip();
            return false;
        }

        if (DataManager.Instance == null)
            return false;

        ItemInfo item = DataManager.Instance.GetItem(itemId);
        if (item == null)
        {
            Unequip();
            return false;
        }

        if (!IsWeaponItem(item))
            return false;

        equippedItemId = itemId;
        weapon = item;

        currentAmmoInMag = Mathf.Max(0, weapon.magazinesize);
        recoilIndex = 0;
        fireCooldown = 0f;
        isReloading = false;
        recoilAccumYaw = 0f;

        AttachWeaponVisual(itemId);

        ApplyWeaponAnimatorSettings(itemId);

        return true;
    }

    public void Unequip()
    {
        equippedItemId = 0;
        weapon = null;

        currentAmmoInMag = 0;
        recoilIndex = 0;
        fireCooldown = 0f;
        isReloading = false;
        recoilAccumYaw = 0f;

        // �⺻ ��Ʈ�ѷ� ����
        if (armsAnimator != null && defaultArmsController != null)
        {
            armsAnimator.runtimeAnimatorController = defaultArmsController;
            CacheParamExistence();
            armsAnimator.Rebind();
            armsAnimator.Update(0f);
            armsAnimator.Play(H_IDLE, -1, 0f);

            // UnEquip Ʈ���Ű� ������ �߻�(��� ���� ����)
            armsAnimator.ResetTrigger(H_UNEQUIP_TRG);
            armsAnimator.SetTrigger(H_UNEQUIP_TRG);
        }

        DetachWeaponVisual();
    }

    private void ApplyWeaponAnimatorSettings(int itemId)
    {
        EnsureArmsAnimator();
        if (armsAnimator == null) return;

        _currentAnimSettings = FindAnimSettings(itemId);

        if (_currentAnimSettings != null && _currentAnimSettings.characterController != null)
            armsAnimator.runtimeAnimatorController = _currentAnimSettings.characterController;

        CacheParamExistence();
        armsAnimator.Rebind();
        armsAnimator.Update(0f);

        // ���̾� ���� (��Ʈ�ѷ� �ٲ�� �ٽ� ����)
        if (forceLayerWeights)
            ForceKinemationLayerWeights();

        armsAnimator.Play(H_IDLE, -1, 0f);

        if (_currentAnimSettings != null && _currentAnimSettings.hasEquipOverride)
            armsAnimator.Play(H_EQUIP_OVR, -1, 0f);
        else
            armsAnimator.Play(H_EQUIP, -1, 0f);
    }

    // ------------------ Fire / Reload ------------------
    private void TryFire()
    {
        if (weapon == null) return;
        if (fireCooldown > 0f) return;
        if (currentAmmoInMag <= 0) return;
        if (muzzle == null) return;

        Fire();

        // Fire �ִ�
        PlayFireAnim(ammoAfterConsume: currentAmmoInMag - 1);

        if (crosshair != null) crosshair.OnFire();

        fireCooldown = 1f / Mathf.Max(0.01f, weapon.fireRate);
        currentAmmoInMag--;
    }

    private void PlayFireAnim(int ammoAfterConsume)
    {
        if (armsAnimator == null) return;

        bool useFire = _currentAnimSettings == null || _currentAnimSettings.useFireClip;
        bool useOut = _currentAnimSettings != null && _currentAnimSettings.hasFireOut;

        if (useFire) armsAnimator.Play(H_FIRE, -1, 0f);
        if (useOut && ammoAfterConsume <= 0) armsAnimator.Play(H_FIREOUT, -1, 0f);
    }

    private IEnumerator ReloadRoutine()
    {
        if (weapon == null) yield break;

        isReloading = true;
        onReloadStart?.Invoke(weapon.reloadTime);

        PlayReloadAnim(isEmpty: currentAmmoInMag <= 0);

        recoilIndex = 0;
        recoilAccumYaw = 0f;

        yield return new WaitForSeconds(weapon.reloadTime);

        currentAmmoInMag = weapon.magazinesize;
        isReloading = false;

        onReloadEnd?.Invoke();
    }

    private void PlayReloadAnim(bool isEmpty)
    {
        if (armsAnimator == null) return;
        armsAnimator.Play(isEmpty ? H_RELOAD_EMPTY : H_RELOAD_TAC, -1, 0f);
    }

    private void Fire()
    {
        if (cachedCam == null) cachedCam = Camera.main;
        if (cachedCam == null) return;

        Vector3 camOrigin = cachedCam.transform.position;
        Vector3 camDir = cachedCam.transform.forward;

        Vector3 hitPoint = camOrigin + camDir * weapon.effectiveRange;
        bool hasHitPoint = false;

        if (Physics.Raycast(camOrigin, camDir, out RaycastHit hit, weapon.effectiveRange, hitMask))
        {
            hasHitPoint = true;
            hitPoint = hit.point;

            if (crosshair != null) crosshair.OnHitConfirm();

            EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>();
            if (enemy != null)
                enemy.TakeDamage(weapon.damage);
        }

        Vector3 origin = muzzle.position;
        Vector3 forward = (hitPoint - origin);

        if (forward.sqrMagnitude < 0.0001f)
            forward = cachedCam.transform.forward;
        else
            forward.Normalize();

        Vector3 recoiledForward = ApplyRecoil(forward);

        int pellets = Mathf.Max(1, weapon.pelletsPerShot);

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = GetSpreadDirection(recoiledForward, weapon.spreadAngle);
            Vector3? visualHit = hasHitPoint ? hitPoint : (Vector3?)null;
            SpawnVisualBullet(origin, dir, visualHit);
        }

        lastFireTime = Time.time;
    }

    // ------------------ Recoil / Spread ------------------
    private Vector3 ApplyRecoil(Vector3 forward)
    {
        float baseYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        if (Time.time - lastFireTime > weapon.recoilResetTime)
        {
            recoilIndex = 0;
            recoilAccumYaw = 0f;
        }
        else
        {
            if (recoilRecoverSpeed > 0f)
                recoilAccumYaw = Mathf.MoveTowards(recoilAccumYaw, 0f, recoilRecoverSpeed * Time.deltaTime);
        }

        float deltaYaw = 0f;

        if (weapon.useRecoilPattern == 1)
        {
            float[] pattern = GetRecoilPatternByItemId(equippedItemId);
            if (pattern != null && pattern.Length > 0)
            {
                deltaYaw += pattern[Mathf.Min(recoilIndex, pattern.Length - 1)];
                recoilIndex++;
            }
        }

        float randomRange = Mathf.Abs((float)weapon.randomRecoilAngle);
        if (randomRange > 0f)
            deltaYaw += Random.Range(-randomRange, randomRange);

        recoilAccumYaw += deltaYaw;

        float finalYaw = baseYaw + recoilAccumYaw;
        float rad = finalYaw * Mathf.Deg2Rad;

        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    private Vector3 GetSpreadDirection(Vector3 forward, float spreadAngle)
    {
        if (spreadAngle <= 0.01f)
            return forward;

        float half = spreadAngle * 0.5f;
        float yawOffset = Random.Range(-half, half);

        float baseYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        float finalYaw = baseYaw + yawOffset;

        float rad = finalYaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
    }

    // ------------------ Bullet Visual ------------------
    private void SpawnVisualBullet(Vector3 origin, Vector3 dir, Vector3? hitPoint = null)
    {
        if (bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab);
        var vb = bullet.GetComponent<VisualBullet>();

        if (vb == null)
        {
            Destroy(bullet);
            return;
        }

        float lt = bulletLifeTime;

        if (hitPoint.HasValue)
        {
            float dist = Vector3.Distance(origin, hitPoint.Value);
            float t = dist / Mathf.Max(0.01f, bulletSpeed);
            lt = Mathf.Max(0.02f, t);
        }

        vb.Init(origin, dir, bulletSpeed, lt, hitPoint);
    }

    // ------------------ Weapon Visual ------------------
    private Transform FindChildRecursive(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == name)
                return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private void AttachWeaponVisual(int itemId)
    {
        if (weaponSocket == null) return;

        ClearWeaponSocketChildren();
        DetachWeaponVisual();

        GameObject prefab = GetWeaponPrefab(itemId);
        if (prefab == null)
        {
            muzzle = null;
            return;
        }

        equippedWeaponGO = Instantiate(prefab, weaponSocket);
        equippedWeaponGO.transform.localPosition = Vector3.zero;
        equippedWeaponGO.transform.localRotation = Quaternion.identity;
        equippedWeaponGO.transform.localScale = Vector3.one;

        ApplyOffset(itemId, equippedWeaponGO.transform);

        muzzle = FindChildRecursive(equippedWeaponGO.transform, MUZZLE_NAME);
    }

    private void ApplyOffset(int itemId, Transform t)
    {
        if (weaponOffsets == null) return;

        for (int i = 0; i < weaponOffsets.Length; i++)
        {
            if (weaponOffsets[i].itemId != itemId) continue;

            t.localPosition = weaponOffsets[i].localPos;
            t.localRotation = Quaternion.Euler(weaponOffsets[i].localEuler);

            Vector3 sc = weaponOffsets[i].localScale;
            t.localScale = (sc == Vector3.zero) ? Vector3.one : sc;
            return;
        }
    }

    private void DetachWeaponVisual()
    {
        if (equippedWeaponGO != null)
            Destroy(equippedWeaponGO);

        equippedWeaponGO = null;
        muzzle = null;
    }

    private GameObject GetWeaponPrefab(int itemId)
    {
        if (weaponPrefabs == null || weaponPrefabs.Length < 5)
            return null;

        switch (itemId)
        {
            case 1101: return weaponPrefabs[0];
            case 1201: return weaponPrefabs[1];
            case 1202: return weaponPrefabs[2];
            case 1301: return weaponPrefabs[3];
            case 1401: return weaponPrefabs[4];
            default: return null;
        }
    }

    private void ClearWeaponSocketChildren()
    {
        if (weaponSocket == null) return;
        for (int i = weaponSocket.childCount - 1; i >= 0; i--)
            Destroy(weaponSocket.GetChild(i).gameObject);
    }

    // ------------------ Anim Settings Lookup ------------------
    private WeaponAnimSettings FindAnimSettings(int itemId)
    {
        if (weaponAnimLinks == null) return null;
        for (int i = 0; i < weaponAnimLinks.Length; i++)
            if (weaponAnimLinks[i].itemId == itemId) return weaponAnimLinks[i].animSettings;
        return null;
    }

    // ------------------ Item Helper ------------------
    private bool IsWeaponItem(ItemInfo item)
    {
        if (item == null) return false;
        return item.id >= 1100 && item.id < 1500;
    }

    // ------------------ Recoil Pattern ------------------
    private float[] GetRecoilPatternByItemId(int itemId)
    {
        switch (itemId)
        {
            case 1101: return new float[] { 0.15f, 0.2f, 0.25f };
            case 1201:
                return new float[] {
                    0.0f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f,
                    0.5f, 0.5f, 0.5f, 0.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f,
                    -1.0f, -1.0f, -1.0f, -1.0f, -0.5f, -0.5f, 0.0f, 1.0f, 2.0f, 2.0f
                };
            case 1202: return new float[] { 0.1f, 0.2f, 0.3f };
            case 1301: return new float[] { 0.6f };
            case 1401: return new float[] { 0.15f, 0.15f };
            default: return new float[0];
        }
    }

    // ------------------ AnimationEvent Receivers (���� ����) ------------------
    public void PlayEquipSound() { }
    public void PlayWeaponSound() { }
    public void PlayReloadSound() { }

    // ------------------ Getters ------------------
    public int GetCurrentAmmo() => currentAmmoInMag;
    public int GetMagazineSize() => weapon != null ? weapon.magazinesize : 0;
    public int GetEquippedItemId() => equippedItemId;
    public bool HasWeaponEquipped() => weapon != null;
}
