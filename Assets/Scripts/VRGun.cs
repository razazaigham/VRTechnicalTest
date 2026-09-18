using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VRGun : XRGrabInteractable
{
    const int MagazineSize = 12;
    const float ReloadRadius = 0.12f;

    [Header("Firing Settings")]
    public Transform muzzlePoint;
    public Transform magazineWell;
    public Transform magazineVisual;
    public ParticleSystem muzzleFlash;
    public AudioSource shootAudio;
    public float fireRate = 0.12f;
    public float weaponRange = 100f;
    public LayerMask hitLayers = ~0;

    [Header("Feedback")]
    [Range(0, 1)] public float hapticAmplitude = 0.55f;
    public float hapticDuration = 0.08f;
    [Range(0, 1)] public float hitHapticAmplitude = 0.75f;
    public float hitHapticDuration = 0.12f;

    float nextFireTime;
    int ammo = MagazineSize;
    bool reloadLatched;
    IXRInteractor firingInteractor;
    InputAction releaseAction;
    AudioClip shotClip;
    AudioClip dryClip;
    AudioClip reloadClip;
    readonly List<IXRInteractor> interactorBuffer = new List<IXRInteractor>(8);
    readonly Queue<GameObject> sparkPool = new Queue<GameObject>(16);
    readonly Queue<GameObject> decalPool = new Queue<GameObject>(16);
    Material sparkMat;
    Material decalMat;

    protected override void Awake()
    {
        base.Awake();
        farAttachMode = InteractableFarAttachMode.Near;
        movementType = MovementType.Instantaneous;
        attachEaseInTime = 0f;
        ResolveRefs();
        EnsureAudio();
        BuildFeedbackMaterials();
        WarmPools();

        if (muzzleFlash != null && !muzzleFlash.gameObject.scene.IsValid())
        {
            Transform parent = muzzlePoint != null ? muzzlePoint : transform;
            muzzleFlash = Instantiate(muzzleFlash, parent);
            muzzleFlash.transform.localPosition = Vector3.zero;
            muzzleFlash.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (hitLayers == 0)
            hitLayers = Physics.DefaultRaycastLayers;

        SetMagazineVisual(true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (releaseAction == null)
        {
            releaseAction = new InputAction("ReleaseGun", InputActionType.Button);
            releaseAction.AddBinding("<Keyboard>/x");
            releaseAction.AddBinding("<Keyboard>/b");
            releaseAction.AddBinding("<XRController>{LeftHand}/primaryButton");
            releaseAction.AddBinding("<XRController>{RightHand}/primaryButton");
        }

        releaseAction.performed += OnReleasePerformed;
        releaseAction.Enable();
    }

    protected override void OnDisable()
    {
        if (releaseAction != null)
        {
            releaseAction.performed -= OnReleasePerformed;
            releaseAction.Disable();
        }

        base.OnDisable();
    }

    void Update()
    {
        if (!isSelected)
        {
            reloadLatched = false;
            return;
        }

        TryMotionReload();
    }

    void OnReleasePerformed(InputAction.CallbackContext context)
    {
        if (isSelected)
            Drop();
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        firingInteractor = args.interactorObject;
        reloadLatched = true;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        if (firingInteractor == args.interactorObject)
            firingInteractor = null;
        reloadLatched = false;
    }

    protected override void OnActivated(ActivateEventArgs args)
    {
        base.OnActivated(args);
        firingInteractor = args.interactorObject;
        TryFire();
    }

    void TryFire()
    {
        if (!isSelected || Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + fireRate;

        if (ammo <= 0)
        {
            PlayClip(dryClip, 0.45f);
            SendHaptic(0.2f, 0.05f);
            return;
        }

        ammo--;
        SetMagazineVisual(ammo > 0);
        Fire();
    }

    void Fire()
    {
        if (muzzleFlash)
            muzzleFlash.Play();
        PlayClip(shotClip, 1f);
        SendHaptic(hapticAmplitude, hapticDuration);

        if (muzzlePoint == null)
            return;

        Vector3 origin = muzzlePoint.position + muzzlePoint.forward * 0.02f;
        if (!Physics.Raycast(origin, muzzlePoint.forward, out RaycastHit hit, weaponRange, hitLayers, QueryTriggerInteraction.Ignore))
            return;
        if (hit.transform != null && hit.transform.IsChildOf(transform))
            return;

        SpawnSpark(hit.point, hit.normal);
        SpawnDecal(hit);
        SendHaptic(hitHapticAmplitude, hitHapticDuration);

        var hitbox = hit.collider.GetComponent<EnemyHitbox>();
        if (hitbox != null)
        {
            hitbox.NotifyHit(hit);
        }
        else if (hit.collider.GetComponentInParent<IDamageable>() is IDamageable damageable)
        {
            damageable.TakeDamage(34f, false, hit);
        }
        else if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
        {
            hit.rigidbody.AddForceAtPosition(
                muzzlePoint.forward * 5f - hit.normal * 3f,
                hit.point,
                ForceMode.Impulse);
        }
    }

    void TryMotionReload()
    {
        if (magazineWell == null || interactionManager == null)
            return;
        if (ammo >= MagazineSize)
            return;

        interactorBuffer.Clear();
        interactionManager.GetRegisteredInteractors(interactorBuffer);

        bool freeHandInWell = false;
        for (int i = 0; i < interactorBuffer.Count; i++)
        {
            IXRInteractor interactor = interactorBuffer[i];
            if (interactor == null || IsHeldBy(interactor))
                continue;

            Transform attach = interactor.GetAttachTransform(this);
            if (attach == null)
                continue;

            Transform heldAttach = firingInteractor != null ? firingInteractor.GetAttachTransform(this) : null;
            if (heldAttach != null && Vector3.Distance(attach.position, heldAttach.position) < 0.08f)
                continue;

            if (Vector3.Distance(attach.position, magazineWell.position) <= ReloadRadius)
            {
                freeHandInWell = true;
                break;
            }
        }

        if (freeHandInWell && !reloadLatched)
        {
            reloadLatched = true;
            Reload();
        }
        else if (!freeHandInWell)
        {
            reloadLatched = false;
        }
    }

    void Reload()
    {
        ammo = MagazineSize;
        SetMagazineVisual(true);
        PlayClip(reloadClip, 0.8f);
        SendHaptic(0.35f, 0.07f);
    }

    void SendHaptic(float amplitude, float duration)
    {
        if (firingInteractor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(amplitude, duration);
    }

    void PlayClip(AudioClip clip, float volume)
    {
        if (shootAudio == null || clip == null)
            return;
        shootAudio.PlayOneShot(clip, volume);
    }

    bool IsHeldBy(IXRInteractor interactor)
    {
        for (int i = 0; i < interactorsSelecting.Count; i++)
        {
            if (ReferenceEquals(interactorsSelecting[i], interactor))
                return true;
        }

        return false;
    }

    void SetMagazineVisual(bool seated)
    {
        if (magazineVisual == null)
            return;

        magazineVisual.localScale = seated
            ? new Vector3(0.03f, 0.07f, 0.05f)
            : new Vector3(0.022f, 0.025f, 0.04f);
    }

    void ResolveRefs()
    {
        if (muzzlePoint == null)
        {
            Transform found = transform.Find("Muzzle");
            if (found != null)
                muzzlePoint = found;
        }

        if (magazineWell == null)
        {
            Transform found = transform.Find("MagazineWell");
            if (found != null)
                magazineWell = found;
        }

        if (magazineVisual == null)
        {
            Transform found = transform.Find("MagazineVisual");
            if (found != null)
                magazineVisual = found;
        }

        if (magazineVisual != null)
        {
            var magRenderer = magazineVisual.GetComponent<Renderer>();
            if (magRenderer != null)
            {
                magRenderer.material.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.22f));
                magRenderer.material.color = new Color(0.18f, 0.18f, 0.22f);
            }
        }

        if (shootAudio == null)
            shootAudio = GetComponent<AudioSource>();
    }

    void EnsureAudio()
    {
        if (shootAudio == null)
            shootAudio = gameObject.AddComponent<AudioSource>();

        shootAudio.playOnAwake = false;
        shootAudio.spatialBlend = 1f;
        shootAudio.minDistance = 0.4f;
        shootAudio.maxDistance = 18f;

        shotClip = CreateBurstClip("GunShot", 0.11f, 40f, 0.7f);
        dryClip = CreateBurstClip("GunDry", 0.06f, 70f, 0.25f);
        reloadClip = CreateBurstClip("GunReload", 0.08f, 28f, 0.4f);
    }

    static AudioClip CreateBurstClip(string clipName, float duration, float decay, float amplitude)
    {
        const int frequency = 22050;
        int samples = Mathf.Max(32, (int)(frequency * duration));
        var clip = AudioClip.Create(clipName, samples, 1, frequency, false);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)frequency;
            float env = Mathf.Exp(-t * decay);
            data[i] = (Random.value * 2f - 1f) * env * amplitude;
        }

        clip.SetData(data, 0);
        return clip;
    }

    void BuildFeedbackMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;

        sparkMat = new Material(shader);
        sparkMat.color = new Color(1f, 0.55f, 0.15f);
        sparkMat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.15f));

        decalMat = new Material(shader);
        decalMat.color = new Color(0.12f, 0.12f, 0.12f);
        decalMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f));
    }

    void WarmPools()
    {
        for (int i = 0; i < 8; i++)
        {
            sparkPool.Enqueue(CreateSpark());
            decalPool.Enqueue(CreateDecal());
        }
    }

    GameObject CreateSpark()
    {
        var go = new GameObject("HitSpark");
        go.SetActive(false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.15f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.18f;
        main.startSpeed = 2.4f;
        main.startSize = 0.025f;
        main.maxParticles = 24;
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.01f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (sparkMat != null)
            renderer.material = sparkMat;
        return go;
    }

    GameObject CreateDecal()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "HitDecal";
        go.SetActive(false);
        Destroy(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>();
        if (decalMat != null)
            renderer.sharedMaterial = decalMat;
        return go;
    }

    void SpawnSpark(Vector3 point, Vector3 normal)
    {
        GameObject go = sparkPool.Count > 0 ? sparkPool.Dequeue() : CreateSpark();
        go.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
        go.SetActive(true);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Clear();
        ps.Play();
        StartCoroutine(Recycle(go, sparkPool, 0.4f));
    }

    void SpawnDecal(RaycastHit hit)
    {
        GameObject go = decalPool.Count > 0 ? decalPool.Dequeue() : CreateDecal();
        go.transform.SetPositionAndRotation(
            hit.point + hit.normal * 0.004f,
            Quaternion.LookRotation(-hit.normal));
        go.transform.localScale = Vector3.one * 0.07f;
        if (hit.rigidbody != null)
            go.transform.SetParent(hit.transform, true);
        else
            go.transform.SetParent(null, true);
        go.SetActive(true);
        StartCoroutine(Recycle(go, decalPool, 8f));
    }

    System.Collections.IEnumerator Recycle(GameObject go, Queue<GameObject> pool, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go == null)
            yield break;
        go.SetActive(false);
        go.transform.SetParent(null, false);
        pool.Enqueue(go);
    }
}
