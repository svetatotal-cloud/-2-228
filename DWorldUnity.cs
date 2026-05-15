using System.Collections.Generic;
using UnityEngine;

// D-World / Dreamworld Unity port.
// Drop this single file into a Unity project, attach it to an empty GameObject,
// press Play. No external images, audio, prefabs, folders, or packages required.
public class DWorldUnity : MonoBehaviour
{
    const float VW = 1600f;
    const float VH = 900f;
    const float BulletMaxDist = 450f;
    const float EnemyBulletNormalDist = 760f;
    const float EnemyBulletBossDist = 920f;
    const float EnemyBulletBossLongDist = 1450f;
    const float BossLongShotChance = 0.20f;
    const float PlayerRegenTime = 25f;
    const float BotHomingChance = 0.20f;
    const float BossHomingChance = 1f / 3f;
    const float HomingFov = 45f * Mathf.Deg2Rad;
    const float HomingTurnSpeed = 4.2f * Mathf.Deg2Rad * 60f;

    enum GameState { Menu, Credits, Play, GameOver }

    class Particle
    {
        public Vector2 pos;
        public Vector2 vel;
        public Color color;
        public float life = 1f;
        public float size;
        public float decay;

        public Particle(Vector2 p, Color c, float speed, float decayValue)
        {
            pos = p;
            color = c;
            float a = Random.value * Mathf.PI * 2f;
            vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
            size = Random.Range(2f, 5f);
            decay = decayValue;
        }

        public bool Update(float dt)
        {
            pos += vel * dt * 60f;
            life -= decay * dt * 60f;
            size *= Mathf.Pow(0.94f, dt * 60f);
            return life > 0f;
        }
    }

    class Loot
    {
        public Vector2 pos;
        public bool hp;
        public Loot(Vector2 p, bool isHp) { pos = p; hp = isHp; }
    }

    class Bullet
    {
        public Vector2 pos;
        public Vector2 vel;
        public float dist;
        public float maxDist;
        public bool bounced;
        public bool enemy;
        public bool boss;
        public bool longShot;
        public bool homing;
        public bool homingActive;
        public float angle;
        public float speed;
    }

    class Robot
    {
        public Vector2 pos;
        public bool boss;
        public int level;
        public int maxHp;
        public int hp;
        public float baseSpeed;
        public float speed;
        public float size;
        public float slowTimer;
        public float shootTimer;
        public float strafeDir;
        public float idealDist;
        public float animOffset;
        public float flashBlindTimer;
        public float longShotFlash;

        public Robot(float x, float y, int roomLevel, bool isBoss)
        {
            pos = new Vector2(x, y);
            boss = isBoss;
            level = roomLevel;
            float hpMul = 1f + level * 0.2f;
            float speedMul = 1f + level * 0.05f;
            int baseHp = boss ? 30 + level * 2 : 3;
            maxHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * hpMul));
            hp = maxHp;
            baseSpeed = (boss ? 1.7f / 1.5f : Random.Range(2.5f, 3.1f) / 1.5f) * speedMul;
            speed = baseSpeed;
            size = boss ? 70f : 22f;
            strafeDir = Random.value < 0.5f ? -1f : 1f;
            idealDist = boss ? 150f : Random.Range(200f, 450f);
            animOffset = Random.value * Mathf.PI * 2f;

            float reduction = level * 0.05f;
            float minShoot = Mathf.Max(0.2f, 1f - reduction);
            float maxShoot = Mathf.Max(0.5f, 3f - reduction);
            if (boss)
            {
                minShoot *= 0.5f;
                maxShoot *= 0.5f;
            }
            else
            {
                minShoot /= 1.5f;
                maxShoot /= 1.5f;
            }
            shootTimer = Random.Range(minShoot, maxShoot);
        }
    }

    GameState state = GameState.Menu;
    int rooms = 1;
    int bestRoom = 1;
    int hp = 3;
    int ammo = 60;
    float light = 100f;
    Vector2 player = new Vector2(VW / 2f, VH / 2f);
    Vector2 playerVelocity;
    float playerAngle;

    readonly List<Rect> walls = new List<Rect>();
    readonly List<Rect> gates = new List<Rect>();
    readonly List<Robot> robots = new List<Robot>();
    readonly List<Bullet> bullets = new List<Bullet>();
    readonly List<Bullet> enemyBullets = new List<Bullet>();
    readonly List<Particle> particles = new List<Particle>();
    readonly List<Loot> loot = new List<Loot>();
    readonly List<Vector2> trail = new List<Vector2>();

    bool paused;
    bool isDashing;
    bool isDying;
    bool fCharging;
    float dashTime;
    float dashCooldown;
    float shootCooldown;
    float shieldTimer;
    float damageFlash;
    float lightFlash;
    float shake;
    float ultCooldown;
    float adrenalineTimer;
    float deathAnimTimer;
    float deathDelayTimer;
    float dashInvisibilityTimer;
    float bossRoomWarningTimer;
    float floorPulse;
    float regenTimer = PlayerRegenTime;
    float musicVolume = 0.5f;
    bool volumeDragging;
    bool mobileAiming;
    Vector2 touchMoveInput;
    Vector2 leftStickCenter;
    Vector2 leftStickKnob;
    Vector2 rightStickCenter;
    Vector2 rightStickKnob;
    Vector2 mobileAimDir = Vector2.right;
    int leftTouchId = -1;
    int rightTouchId = -1;

    Texture2D whiteTex;
    Texture2D circleTex;
    Texture2D softCircleTex;
    GUIStyle titleStyle;
    GUIStyle labelStyle;
    Matrix4x4 oldMatrix;

    static readonly Color Bg = new Color32(2, 2, 5, 255);
    static readonly Color Wall = new Color32(20, 18, 18, 255);
    static readonly Color WallGlow = new Color32(35, 30, 30, 255);
    static readonly Color Dash = new Color32(0, 140, 180, 255);
    static readonly Color LightUi = new Color32(180, 180, 120, 255);
    static readonly Color Blood = new Color32(110, 0, 0, 255);
    static readonly Color Reload = new Color32(160, 130, 0, 255);
    static readonly Color Grid = new Color32(10, 18, 24, 255);
    static readonly Color GridHot = new Color32(25, 45, 55, 255);
    static readonly Color UiEdge = new Color32(230, 40, 30, 255);
    static readonly Color Boss = new Color32(210, 20, 20, 255);
    static readonly Color BossCore = new Color32(255, 80, 30, 255);
    static readonly Color EnemyGlow = new Color32(160, 0, 0, 255);
    static readonly Color DashGhost = new Color32(90, 230, 255, 255);
    static readonly Color Gold = new Color32(230, 180, 40, 255);

    Rect BtnNew => new Rect(VW / 2f - 140f, 430f, 280f, 60f);
    Rect BtnCredits => new Rect(VW / 2f - 140f, 510f, 280f, 60f);
    Rect BtnExit => new Rect(VW / 2f - 140f, 590f, 280f, 60f);
    Rect BtnBackCredits => new Rect(VW / 2f - 140f, 750f, 280f, 60f);
    Rect BtnRespawn => new Rect(VW / 2f - 140f, 520f, 280f, 60f);
    Rect BtnToMenu => new Rect(VW / 2f - 140f, 600f, 280f, 60f);
    Rect VolSlider => new Rect(VW - 250f, 40f, 200f, 10f);
    Rect MobileDashButton => new Rect(VW - 360f, VH - 205f, 82f, 82f);
    Rect MobileFlashButton => new Rect(VW - 260f, VH - 285f, 82f, 82f);
    Rect MobileUltButton => new Rect(VW - 160f, VH - 205f, 94f, 94f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        if (FindObjectOfType<DWorldUnity>() != null)
            return;

        var go = new GameObject("D-World Unity Runtime");
        go.AddComponent<DWorldUnity>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        Application.targetFrameRate = 60;
        Input.multiTouchEnabled = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        floorPulse = Random.value * Mathf.PI * 2f;
        BuildTextures();
        BuildStyles();
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Bg;
        }
    }

    void BuildTextures()
    {
        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();

        circleTex = MakeCircleTexture(128, false);
        softCircleTex = MakeCircleTexture(128, true);
    }

    Texture2D MakeCircleTexture(int size, bool soft)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = (size - 2) * 0.5f;
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = soft ? Mathf.Clamp01(1f - d / r) : (d <= r ? 1f : 0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    void BuildStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.red;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = Color.white;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
        HandleMobileTouches();
        Vector2 mouse = MouseVirtual();
        Vector2 aimPoint = mobileAiming ? player + mobileAimDir * BulletMaxDist : mouse;
        playerAngle = Mathf.Atan2(aimPoint.y - player.y, aimPoint.x - player.x);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == GameState.Play) paused = !paused;
            else if (state == GameState.Credits) state = GameState.Menu;
        }

        if (state != GameState.Play || paused)
            return;

        UpdatePlay(dt, mouse);
    }

    void UpdatePlay(float dt, Vector2 mouse)
    {
        if (isDying)
        {
            if (deathAnimTimer > 0f)
            {
                deathAnimTimer -= dt;
                shake = 10f;
                for (int i = 0; i < 3; i++)
                    particles.Add(new Particle(player, new Color(Random.Range(0.6f, 1f), 0f, 0f), Random.Range(2f, 7f), 0.04f));
            }
            else if (deathDelayTimer > 0f)
            {
                deathDelayTimer -= dt;
            }
            else
            {
                state = GameState.GameOver;
            }
            UpdateParticles(dt);
            return;
        }

        TickTimers(dt);
        HandlePlayerInput(dt, mouse);
        HandleLoot();
        UpdateRobots(dt);
        UpdateEnemyBullets(dt);
        UpdatePlayerBullets(dt);
        UpdateParticles(dt);

        if (robots.Count == 0 && (player.x < 0f || player.x > VW || player.y < 0f || player.y > VH))
            GenerateLevel(true);
    }

    void TickTimers(float dt)
    {
        if (bossRoomWarningTimer > 0f) bossRoomWarningTimer -= dt;
        if (dashInvisibilityTimer > 0f) dashInvisibilityTimer -= dt;
        if (shake > 0f) shake *= Mathf.Pow(0.9f, dt * 60f);
        if (light < 100f) light += 3.6f * dt;
        if (damageFlash > 0f) damageFlash -= 3f * dt;
        if (lightFlash > 0f) lightFlash -= 4.8f * dt;
        if (dashCooldown > 0f) dashCooldown -= dt;
        if (shootCooldown > 0f) shootCooldown -= dt;
        if (shieldTimer > 0f) shieldTimer -= dt;
        if (ultCooldown > 0f) ultCooldown -= dt;
        if (adrenalineTimer > 0f)
        {
            adrenalineTimer -= dt;
            if (ultCooldown > 0f) ultCooldown -= dt;
        }

        if (hp < 3)
        {
            regenTimer -= dt;
            if (regenTimer <= 0f)
            {
                hp = Mathf.Min(3, hp + 1);
                regenTimer = PlayerRegenTime;
                lightFlash = Mathf.Max(lightFlash, 0.6f);
                for (int i = 0; i < 35; i++)
                    particles.Add(new Particle(player, new Color32(40, 180, 70, 255), Random.Range(1f, 4f), 0.04f));
            }
        }
        else
        {
            regenTimer = PlayerRegenTime;
        }
    }

    void HandlePlayerInput(float dt, Vector2 mouse)
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) && dashCooldown <= 0f)
            TryDash();

        if (Input.GetKeyDown(KeyCode.R))
            TryFlash();

        if (Input.GetKeyDown(KeyCode.Space))
            TryRadialUlt();

        if (Input.GetMouseButtonDown(1) && ultCooldown <= 0f)
        {
            ultCooldown = adrenalineTimer > 0f ? 5f : 10f;
            shake = 20f;
            for (int a = -3; a <= 3; a++)
                SendBullet(playerAngle + a * 0.15f, BulletMaxDist);
        }

        if (Input.GetKey(KeyCode.F)) fCharging = true;
        if (fCharging && Input.GetKeyUp(KeyCode.F))
        {
            fCharging = false;
            if (ultCooldown <= 0f)
            {
                Vector2 oldPlayer = player;
                ultCooldown = adrenalineTimer > 0f ? 5f : 10f;
                shake = 35f;
                player = mouse;
                for (int i = 0; i < 25; i++)
                    SendBullet((Mathf.PI * 2f / 25f) * i, BulletMaxDist);
                player = oldPlayer;
            }
        }

        if (!UseMobileControls() && Input.GetMouseButtonDown(0) && shootCooldown <= 0f && ammo > 0 && !VolSlider.Contains(mouse))
        {
            float d = Mathf.Min(Vector2.Distance(mouse, player), BulletMaxDist);
            ammo--;
            shootCooldown = 0.25f;
            shake = 3f;
            SendBullet(playerAngle, d);
        }

        if (isDashing)
        {
            dashInvisibilityTimer = Mathf.Max(dashInvisibilityTimer, dashTime);
            dashTime -= dt;
            for (int i = 0; i < 5; i++)
                particles.Add(new Particle(player, Dash, Random.Range(1f, 3f), 0.04f));
            for (int i = 0; i < 2; i++)
                particles.Add(new Particle(player, DashGhost, Random.Range(0.5f, 2f), 0.04f));
            if (dashTime <= 0f) isDashing = false;
        }

        float dx = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        float dy = (Input.GetKey(KeyCode.S) ? 1f : 0f) - (Input.GetKey(KeyCode.W) ? 1f : 0f);
        Vector2 input = new Vector2(dx, dy);
        if (UseMobileControls() && touchMoveInput.sqrMagnitude > input.sqrMagnitude)
            input = touchMoveInput;
        if (input.sqrMagnitude > 1f) input.Normalize();

        float speed = isDashing ? 26f : 6.8f;
        if (adrenalineTimer > 0f) speed *= 1.25f;
        playerVelocity = input * speed * dt * 60f;

        if (input.sqrMagnitude > 0f)
        {
            trail.Add(player);
            if (trail.Count > 18) trail.RemoveAt(0);
            if (Random.value < 0.4f)
                particles.Add(new Particle(player, new Color32(60, 60, 60, 255), 0.5f, 0.04f));
        }
        else if (trail.Count > 0)
        {
            trail.RemoveAt(0);
        }

        MovePlayerAxis(new Vector2(playerVelocity.x, 0f));
        MovePlayerAxis(new Vector2(0f, playerVelocity.y));
    }

    void HandleMobileTouches()
    {
        if (!UseMobileControls() || state != GameState.Play || paused || isDying)
        {
            touchMoveInput = Vector2.zero;
            mobileAiming = false;
            leftTouchId = -1;
            rightTouchId = -1;
            return;
        }

        bool leftSeen = false;
        bool rightSeen = false;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            Vector2 virtualPos = ScreenToVirtual(touch.position);
            if (touch.phase == TouchPhase.Began)
            {
                if (MobileDashButton.Contains(virtualPos))
                {
                    TryDash();
                    continue;
                }
                if (MobileFlashButton.Contains(virtualPos))
                {
                    TryFlash();
                    continue;
                }
                if (MobileUltButton.Contains(virtualPos))
                {
                    TryRadialUlt();
                    continue;
                }
                if (virtualPos.x < VW * 0.48f && leftTouchId == -1)
                {
                    leftTouchId = touch.fingerId;
                    leftStickCenter = virtualPos;
                    leftStickKnob = virtualPos;
                }
                else if (rightTouchId == -1)
                {
                    rightTouchId = touch.fingerId;
                    rightStickCenter = virtualPos;
                    rightStickKnob = virtualPos;
                    mobileAiming = true;
                }
            }

            if (touch.fingerId == leftTouchId)
            {
                leftSeen = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                Vector2 delta = virtualPos - leftStickCenter;
                touchMoveInput = Vector2.ClampMagnitude(delta / 95f, 1f);
                leftStickKnob = leftStickCenter + Vector2.ClampMagnitude(delta, 95f);
            }

            if (touch.fingerId == rightTouchId)
            {
                rightSeen = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                Vector2 delta = virtualPos - rightStickCenter;
                if (delta.sqrMagnitude > 20f * 20f)
                    mobileAimDir = delta.normalized;
                rightStickKnob = rightStickCenter + Vector2.ClampMagnitude(delta, 100f);
                mobileAiming = rightSeen;

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    ShootMobile();
                    rightTouchId = -1;
                    mobileAiming = false;
                }
            }
        }

        if (!leftSeen)
        {
            leftTouchId = -1;
            touchMoveInput = Vector2.zero;
        }
        if (!rightSeen && rightTouchId != -1)
        {
            rightTouchId = -1;
            mobileAiming = false;
        }
    }

    void TryDash()
    {
        if (dashCooldown > 0f)
            return;

        isDashing = true;
        dashTime = 0.18f;
        dashCooldown = 1.5f;
        dashInvisibilityTimer = 0.18f;
    }

    void TryFlash()
    {
        if (light < 30f)
            return;

        light -= 30f;
        lightFlash = 1f;
        foreach (Robot r in robots)
        {
            if (Vector2.Distance(r.pos, player) < 500f)
            {
                r.slowTimer = 4f;
                r.flashBlindTimer = 4f;
                for (int i = 0; i < 25; i++)
                    particles.Add(new Particle(r.pos, LightUi, 4f, 0.04f));
            }
        }
    }

    void TryRadialUlt()
    {
        if (ultCooldown > 0f)
            return;

        ultCooldown = adrenalineTimer > 0f ? 5f : 10f;
        shake = 35f;
        for (int i = 0; i < 25; i++)
            SendBullet((Mathf.PI * 2f / 25f) * i, BulletMaxDist);
    }

    void ShootMobile()
    {
        if (shootCooldown > 0f || ammo <= 0)
            return;

        ammo--;
        shootCooldown = 0.25f;
        shake = 3f;
        SendBullet(Mathf.Atan2(mobileAimDir.y, mobileAimDir.x), BulletMaxDist);
    }

    void MovePlayerAxis(Vector2 delta)
    {
        Vector2 old = player;
        player += delta;
        bool gatesBlock = robots.Count > 0;
        if (CollidesWithWalls(new Rect(player.x - 14f, player.y - 14f, 28f, 28f), gatesBlock))
            player = old;
    }

    void HandleLoot()
    {
        for (int i = loot.Count - 1; i >= 0; i--)
        {
            if (Vector2.Distance(loot[i].pos, player) < 45f)
            {
                if (loot[i].hp) hp = Mathf.Min(3, hp + 1);
                else ammo += 15;
                for (int p = 0; p < 15; p++)
                    particles.Add(new Particle(loot[i].pos, Color.gray, 4f, 0.04f));
                loot.RemoveAt(i);
            }
        }
    }

    void UpdateRobots(float dt)
    {
        for (int i = robots.Count - 1; i >= 0; i--)
        {
            Robot r = robots[i];
            UpdateRobot(r, dt);
            if (Vector2.Distance(r.pos, player) < r.size + 15f && shieldTimer <= 0f && !isDashing)
                DamagePlayer();
        }
    }

    void UpdateRobot(Robot r, float dt)
    {
        if (r.slowTimer > 0f)
        {
            r.slowTimer -= dt;
            r.speed = r.baseSpeed * 0.35f;
            r.flashBlindTimer = Mathf.Max(r.flashBlindTimer, 0.15f);
        }
        else
        {
            r.speed = r.baseSpeed;
        }
        if (r.flashBlindTimer > 0f) r.flashBlindTimer -= dt;
        if (r.longShotFlash > 0f) r.longShotFlash -= dt;

        Vector2 toPlayer = player - r.pos;
        float dist = Mathf.Max(0.001f, toPlayer.magnitude);
        float angleToPlayer = Mathf.Atan2(toPlayer.y, toPlayer.x);
        float moveAngle = angleToPlayer;
        if (dist < r.idealDist) moveAngle -= 1.1f * r.strafeDir;
        else if (dist > r.idealDist + 60f) moveAngle += 0.4f * r.strafeDir;

        Vector2 move = new Vector2(Mathf.Cos(moveAngle), Mathf.Sin(moveAngle)) * r.speed * dt * 60f;
        Rect probe = new Rect(r.pos.x + move.x * 15f - 10f, r.pos.y + move.y * 15f - 10f, 20f, 20f);
        if (CollidesWithWalls(probe, false)) r.strafeDir *= -1f;

        r.shootTimer -= dt;
        bool hidden = isDashing || dashInvisibilityTimer > 0f;
        bool blinded = r.flashBlindTimer > 0f || r.slowTimer > 0f;
        if (r.shootTimer <= 0f && !hidden && !blinded)
        {
            Vector2 target = player + playerVelocity * 12f;
            float fireAng = Mathf.Atan2(target.y - r.pos.y, target.x - r.pos.x);
            float enemySpeed = 7.5f;
            float maxD = EnemyBulletNormalDist;
            bool longShot = false;
            bool homing = Random.value < BotHomingChance;
            if (r.boss)
            {
                enemySpeed = 8.8f;
                maxD = EnemyBulletBossDist;
                homing = Random.value < BossHomingChance;
                if (Random.value < BossLongShotChance)
                {
                    enemySpeed = 10.5f;
                    maxD = EnemyBulletBossLongDist;
                    longShot = true;
                    r.longShotFlash = 0.35f;
                }
            }

            enemyBullets.Add(new Bullet
            {
                pos = r.pos,
                vel = new Vector2(Mathf.Cos(fireAng), Mathf.Sin(fireAng)) * enemySpeed,
                maxDist = maxD,
                enemy = true,
                boss = r.boss,
                longShot = longShot,
                homing = homing,
                homingActive = homing,
                angle = fireAng,
                speed = enemySpeed
            });

            float reduction = r.level * 0.05f;
            float minCd = Mathf.Max(0.2f, 1.2f - reduction);
            float maxCd = Mathf.Max(0.5f, 2.8f - reduction);
            if (r.boss) { minCd *= 0.5f; maxCd *= 0.5f; }
            else { minCd /= 1.5f; maxCd /= 1.5f; }
            r.shootTimer = Random.Range(minCd, maxCd);
        }
        else if (r.shootTimer <= 0f && (hidden || blinded))
        {
            r.shootTimer = 0.18f;
        }

        for (int i = 0; i < robots.Count; i++)
        {
            Robot other = robots[i];
            if (other == r) continue;
            float d = Vector2.Distance(r.pos, other.pos);
            if (d < r.size + other.size + 15f)
                move += (r.pos - other.pos).normalized * 1.2f * dt * 60f;
        }

        MoveRobotAxis(r, new Vector2(move.x, 0f));
        MoveRobotAxis(r, new Vector2(0f, move.y));
    }

    void MoveRobotAxis(Robot r, Vector2 delta)
    {
        Vector2 old = r.pos;
        r.pos += delta;
        if (CollidesWithWalls(new Rect(r.pos.x - r.size, r.pos.y - r.size, r.size * 2f, r.size * 2f), true))
            r.pos = old;
    }

    void UpdateEnemyBullets(float dt)
    {
        for (int i = enemyBullets.Count - 1; i >= 0; i--)
        {
            Bullet b = enemyBullets[i];
            if (b.homingActive)
            {
                if (isDashing)
                {
                    b.homingActive = false;
                }
                else
                {
                    float targetAng = Mathf.Atan2(player.y - b.pos.y, player.x - b.pos.x);
                    float diff = AngleDiff(targetAng, b.angle);
                    if (Mathf.Abs(diff) <= HomingFov)
                    {
                        float turn = Mathf.Clamp(diff, -HomingTurnSpeed * dt, HomingTurnSpeed * dt);
                        b.angle += turn;
                        b.vel = new Vector2(Mathf.Cos(b.angle), Mathf.Sin(b.angle)) * b.speed;
                    }
                    else
                    {
                        b.homingActive = false;
                    }
                }
            }

            b.pos += b.vel * dt * 60f;
            b.dist += b.vel.magnitude * dt * 60f;
            if (b.dist > b.maxDist)
            {
                SpawnBulletBreak(b.pos, b.longShot ? Gold : Blood, b.homing ? 15 : 10, 4f);
                enemyBullets.RemoveAt(i);
                continue;
            }
            if (PointInAnyWall(b.pos))
            {
                SpawnBulletBreak(b.pos, new Color32(120, 40, 40, 255), 9, 3f);
                enemyBullets.RemoveAt(i);
                continue;
            }
            if (Vector2.Distance(b.pos, player) < 22f && shieldTimer <= 0f && !isDashing)
            {
                enemyBullets.RemoveAt(i);
                DamagePlayer();
            }
        }
    }

    void UpdatePlayerBullets(float dt)
    {
        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            Bullet b = bullets[i];
            Vector2 oldPos = b.pos;
            b.pos += b.vel * dt * 60f;
            b.dist += 22f * dt * 60f;

            Rect hitWall = new Rect();
            bool hit = false;
            foreach (Rect w in walls)
            {
                if (w.Contains(b.pos))
                {
                    hitWall = w;
                    hit = true;
                    break;
                }
            }

            if (hit)
            {
                if (!b.bounced)
                {
                    b.maxDist *= 1.5f;
                    b.bounced = true;
                }
                if (oldPos.x < hitWall.xMin || oldPos.x > hitWall.xMax) b.vel.x *= -1f;
                else b.vel.y *= -1f;
                b.pos = oldPos;
                for (int p = 0; p < 5; p++)
                    particles.Add(new Particle(b.pos, Color.gray, 2f, 0.04f));
            }

            if (b.dist > b.maxDist)
            {
                SpawnBulletBreak(b.pos, Reload, 14, 4f);
                bullets.RemoveAt(i);
                continue;
            }

            bool removed = false;
            for (int rIndex = robots.Count - 1; rIndex >= 0; rIndex--)
            {
                Robot r = robots[rIndex];
                if (Vector2.Distance(b.pos, r.pos) < r.size + 10f)
                {
                    r.hp--;
                    for (int p = 0; p < 10; p++)
                        particles.Add(new Particle(b.pos, Blood, 4f, 0.04f));
                    bullets.RemoveAt(i);
                    removed = true;
                    if (r.hp <= 0)
                    {
                        for (int p = 0; p < 40; p++)
                            particles.Add(new Particle(r.pos, Blood, Random.Range(2f, 8f), 0.04f));
                        loot.Add(new Loot(r.pos, Random.value < 0.2f));
                        robots.RemoveAt(rIndex);
                    }
                    break;
                }
            }
            if (removed) continue;
        }
    }

    void UpdateParticles(float dt)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            if (!particles[i].Update(dt))
                particles.RemoveAt(i);
        }
    }

    void DamagePlayer()
    {
        int oldHp = hp;
        hp--;
        damageFlash = 1f;
        shieldTimer = 1.1f;
        shake = 15f;
        if (oldHp > 1 && hp == 1) adrenalineTimer = 5f;
        for (int i = 0; i < 20; i++)
            particles.Add(new Particle(player, Blood, 6f, 0.04f));
        if (hp <= 0 && !isDying)
        {
            if (rooms > bestRoom) bestRoom = rooms;
            isDying = true;
            deathAnimTimer = 0.8f;
            deathDelayTimer = 1f;
        }
    }

    void GenerateLevel(bool nextRoom)
    {
        if (nextRoom)
        {
            rooms++;
            if (rooms > bestRoom) bestRoom = rooms;
        }
        else
        {
            if (rooms > bestRoom) bestRoom = rooms;
            rooms = 1;
            hp = 3;
            ammo = 60;
            light = 100f;
            adrenalineTimer = 0f;
            isDying = false;
            deathAnimTimer = 0f;
            deathDelayTimer = 0f;
            dashInvisibilityTimer = 0f;
            regenTimer = PlayerRegenTime;
        }

        walls.Clear();
        gates.Clear();
        robots.Clear();
        bullets.Clear();
        enemyBullets.Clear();
        particles.Clear();
        loot.Clear();
        trail.Clear();

        const float t = 45f;
        const float h = 200f;
        walls.Add(new Rect(0f, 0f, VW / 2f - h, t));
        walls.Add(new Rect(VW / 2f + h, 0f, VW / 2f - h, t));
        walls.Add(new Rect(0f, VH - t, VW / 2f - h, t));
        walls.Add(new Rect(VW / 2f + h, VH - t, VW / 2f - h, t));
        walls.Add(new Rect(0f, 0f, t, VH / 2f - h));
        walls.Add(new Rect(0f, VH / 2f + h, t, VH / 2f - h));
        walls.Add(new Rect(VW - t, 0f, t, VH / 2f - h));
        walls.Add(new Rect(VW - t, VH / 2f + h, t, VH / 2f - h));

        gates.Add(new Rect(VW / 2f - h, 0f, h * 2f, t));
        gates.Add(new Rect(VW / 2f - h, VH - t, h * 2f, t));
        gates.Add(new Rect(0f, VH / 2f - h, t, h * 2f));
        gates.Add(new Rect(VW - t, VH / 2f - h, t, h * 2f));

        bool bossRoom = rooms % 5 == 0;
        if (bossRoom) bossRoomWarningTimer = 2.4f;
        if (!bossRoom)
        {
            for (int i = 0; i < 12; i++)
            {
                Rect w = new Rect(Random.Range(250f, VW - 350f), Random.Range(200f, VH - 300f), 90f, 90f);
                bool tooClose = false;
                Rect inflated = Inflate(w, 180f, 180f);
                foreach (Rect other in walls)
                    if (Overlaps(inflated, other)) tooClose = true;
                if (!tooClose && !Overlaps(Inflate(w, 200f, 200f), new Rect(VW / 2f, VH / 2f, 1f, 1f)))
                    walls.Add(w);
            }
        }

        if (bossRoom)
        {
            robots.Add(new Robot(VW - 200f, VH / 2f, rooms, true));
        }
        else
        {
            int count = 5 + rooms / 2;
            for (int i = 0; i < count; i++)
                robots.Add(new Robot(Random.value < 0.5f ? 150f : VW - 150f, Random.value < 0.5f ? 150f : VH - 150f, rooms, false));
        }

        player = new Vector2(VW / 2f, VH / 2f);
        shieldTimer = 1f;
        paused = false;
    }

    void SendBullet(float angle, float maxDist)
    {
        bullets.Add(new Bullet
        {
            pos = player,
            vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 22f,
            maxDist = maxDist,
            bounced = false
        });
    }

    void SpawnBulletBreak(Vector2 pos, Color color, int count, float speed)
    {
        for (int i = 0; i < count; i++)
            particles.Add(new Particle(pos, color, Random.Range(1f, speed), Random.Range(0.035f, 0.07f)));
    }

    bool CollidesWithWalls(Rect rect, bool includeGates)
    {
        foreach (Rect w in walls)
            if (Overlaps(rect, w)) return true;
        if (includeGates)
            foreach (Rect g in gates)
                if (Overlaps(rect, g)) return true;
        return false;
    }

    bool PointInAnyWall(Vector2 p)
    {
        foreach (Rect w in walls)
            if (w.Contains(p)) return true;
        return false;
    }

    static Rect Inflate(Rect r, float x, float y)
    {
        return new Rect(r.x - x / 2f, r.y - y / 2f, r.width + x, r.height + y);
    }

    static bool Overlaps(Rect a, Rect b)
    {
        return a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
    }

    static float AngleDiff(float a, float b)
    {
        return Mathf.Repeat(a - b + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
    }

    Vector2 MouseVirtual()
    {
        return ScreenToVirtual(Input.mousePosition);
    }

    Vector2 ScreenToVirtual(Vector2 screenPos)
    {
        GetGameViewport(out float scale, out float ox, out float oy);
        return new Vector2((screenPos.x - ox) / scale, (Screen.height - screenPos.y - oy) / scale);
    }

    bool UseMobileControls()
    {
        return Application.isMobilePlatform || Input.touchSupported;
    }

    void GetGameViewport(out float scale, out float ox, out float oy)
    {
        Rect safe = Screen.safeArea;
        if (safe.width <= 0f || safe.height <= 0f)
            safe = new Rect(0f, 0f, Screen.width, Screen.height);

        scale = Mathf.Min(safe.width / VW, safe.height / VH);
        ox = safe.x + (safe.width - VW * scale) * 0.5f;
        oy = Screen.height - safe.yMax + (safe.height - VH * scale) * 0.5f;
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout && Event.current.type != EventType.MouseDown && Event.current.type != EventType.MouseDrag && Event.current.type != EventType.MouseUp)
            return;

        GetGameViewport(out float scale, out float ox, out float oy);
        oldMatrix = GUI.matrix;
        DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), Color.black);
        GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

        DrawRect(new Rect(0f, 0f, VW, VH), Bg);
        DrawFloorGrid();

        if (state == GameState.Menu) DrawMenu();
        else if (state == GameState.Credits) DrawCredits();
        else if (state == GameState.GameOver) DrawGameOver();
        else DrawPlay();

        GUI.matrix = oldMatrix;
    }

    void DrawMenu()
    {
        float t = Time.time * 4f;
        DrawSoftCircle(new Vector2(VW / 2f, 260f), 230f, new Color(0.47f, 0f, 0f, 0.14f));
        DrawText("D-WORLD 11", new Rect(0f, 175f + Mathf.Sin(t) * 15f, VW, 140f), 82, Color.red, titleStyle);
        DrawText("SMART AI UNITY EDITION", new Rect(0f, 300f, VW, 45f), 26, Color.white, labelStyle);
        DrawText("RECORD: ROOM " + bestRoom, new Rect(0f, 360f, VW, 45f), 30, Gold, labelStyle);
        DrawButton(BtnNew, "NEW GAME", false);
        DrawButton(BtnCredits, "CREDITS", false);
        DrawButton(BtnExit, "EXIT GAME", true);
        DrawVignette();

        if (GUI.Button(BtnNew, "", GUIStyle.none)) { GenerateLevel(false); state = GameState.Play; }
        if (GUI.Button(BtnCredits, "", GUIStyle.none)) state = GameState.Credits;
        if (GUI.Button(BtnExit, "", GUIStyle.none)) Application.Quit();
    }

    void DrawCredits()
    {
        DrawSoftCircle(new Vector2(VW / 2f, 470f), 320f, new Color(0.5f, 0f, 0f, 0.12f));
        DrawText("DEVELOPMENT TEAM", new Rect(0f, 155f, VW, 90f), 60, Color.red, titleStyle);
        DrawText("CODER:", new Rect(0f, 330f, VW, 45f), 32, Color.white, labelStyle);
        DrawText("@kovh1kk (Vanya)", new Rect(0f, 382f, VW, 55f), 40, Color.red, labelStyle);
        DrawText("HELPER:", new Rect(0f, 490f, VW, 45f), 32, Color.white, labelStyle);
        DrawText("@WipeManchik (spix)", new Rect(0f, 540f, VW, 55f), 40, Color.red, labelStyle);
        DrawText("OUR TELEGRAM CHANNEL:", new Rect(0f, 645f, VW, 35f), 24, Color.gray, labelStyle);
        DrawText("@spixdevsTM", new Rect(0f, 685f, VW, 55f), 34, new Color32(255, 150, 30, 255), labelStyle);
        DrawButton(BtnBackCredits, "BACK", false);
        DrawVignette();
        if (GUI.Button(BtnBackCredits, "", GUIStyle.none)) state = GameState.Menu;
    }

    void DrawGameOver()
    {
        DrawRect(new Rect(0f, 0f, VW, VH), new Color(0.08f, 0f, 0f, 0.75f));
        DrawSoftCircle(new Vector2(VW / 2f, 320f), 300f, new Color(0.65f, 0f, 0f, 0.22f));
        DrawText("YOU ARE DEAD", new Rect(0f, 190f, VW, 120f), 84, Color.red, titleStyle);
        DrawText("YOU REACHED ROOM: " + rooms, new Rect(0f, 355f, VW, 55f), 38, Color.white, labelStyle);
        DrawText("YOUR RECORD: " + bestRoom, new Rect(0f, 420f, VW, 45f), 32, Gold, labelStyle);
        DrawButton(BtnRespawn, "RESPAWN", false);
        DrawButton(BtnToMenu, "MAIN MENU", false);
        DrawVignette();
        if (GUI.Button(BtnRespawn, "", GUIStyle.none)) { GenerateLevel(false); state = GameState.Play; }
        if (GUI.Button(BtnToMenu, "", GUIStyle.none)) state = GameState.Menu;
    }

    void DrawPlay()
    {
        Vector2 offset = shake > 0.1f ? new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake)) : Vector2.zero;

        foreach (Particle p in particles)
            DrawCircle(p.pos + offset, Mathf.Max(1f, p.size), WithAlpha(p.color, Mathf.Clamp01(p.life)));

        foreach (Rect w in walls)
            DrawWall(w, offset);

        Color gateColor = robots.Count > 0 ? new Color32(120, 0, 0, 255) : new Color32(0, 150, 60, 255);
        foreach (Rect gate in gates)
        {
            DrawRectOutline(Move(gate, offset), gateColor, 2f);
            DrawRectOutline(Move(Inflate(gate, 12f, 12f), offset), WithAlpha(gateColor, 0.18f), 2f);
        }

        DrawLoot(offset);
        DrawRobots(offset);
        DrawBullets(offset);
        DrawAim(offset);
        DrawPlayer(offset);
        DrawHud();
        DrawMobileControls();

        if (paused)
        {
            DrawRect(new Rect(0f, 0f, VW, VH), new Color(0f, 0f, 0f, 0.78f));
            DrawText("PAUSED", new Rect(0f, VH / 2f - 70f, VW, 140f), 80, Color.white, labelStyle);
        }
        if (lightFlash > 0f) DrawRect(new Rect(0f, 0f, VW, VH), new Color(1f, 1f, 1f, Mathf.Clamp01(lightFlash) * 0.4f));
        if (damageFlash > 0f) DrawRect(new Rect(0f, 0f, VW, VH), new Color(0.65f, 0f, 0f, Mathf.Clamp01(damageFlash) * 0.3f));
        DrawVignette();
    }

    void DrawFloorGrid()
    {
        float pulse = 0.07f + Mathf.Sin(Time.time * 2f + floorPulse) * 0.03f;
        for (float x = 0f; x <= VW + 80f; x += 80f)
            DrawLine(new Vector2(x, 0f), new Vector2(x, VH), WithAlpha(Grid, x % 160f == 0f ? pulse + 0.05f : pulse), 1f);
        for (float y = 0f; y <= VH + 80f; y += 80f)
            DrawLine(new Vector2(0f, y), new Vector2(VW, y), WithAlpha(Grid, y % 160f == 0f ? pulse + 0.05f : pulse), 1f);
        DrawCircleOutline(new Vector2(VW / 2f, VH / 2f), 260f, WithAlpha(GridHot, 0.09f), 2f);
        DrawCircleOutline(new Vector2(VW / 2f, VH / 2f), 420f, WithAlpha(GridHot, 0.06f), 1f);
    }

    void DrawWall(Rect r, Vector2 offset)
    {
        Rect moved = Move(r, offset);
        DrawRect(moved, Wall);
        DrawRectOutline(moved, WallGlow, 2f);
        DrawLine(new Vector2(moved.xMin, moved.yMin), new Vector2(moved.xMax, moved.yMin), new Color32(55, 45, 42, 255), 2f);
        DrawLine(new Vector2(moved.xMin, moved.yMax), new Vector2(moved.xMax, moved.yMax), new Color32(5, 5, 7, 255), 2f);
        if (moved.width > 60f && moved.height > 20f)
        {
            for (float x = moved.xMin + 18f; x < moved.xMax; x += 38f)
                DrawLine(new Vector2(x, moved.yMin + 5f), new Vector2(x, moved.yMax - 5f), new Color32(12, 12, 14, 255), 1f);
        }
    }

    void DrawLoot(Vector2 offset)
    {
        float off = Mathf.Sin(Time.time * 10f) * 5f;
        foreach (Loot lt in loot)
        {
            Color c = lt.hp ? Blood : Reload;
            Vector2 p = lt.pos + offset + new Vector2(0f, off);
            DrawSoftCircle(p, 26f, WithAlpha(c, 0.15f));
            DrawRectOutline(new Rect(p.x - 10f, p.y - 10f, 20f, 20f), Color.gray, 1f);
            DrawRect(new Rect(p.x - 6f, p.y - 6f, 12f, 12f), c);
        }
    }

    void DrawRobots(Vector2 offset)
    {
        foreach (Robot r in robots)
        {
            float pulse = Mathf.Sin(Time.time * 5f + r.animOffset) * 3f;
            float currSize = r.size + pulse;
            Vector2 p = r.pos + offset;
            DrawSoftCircle(p, currSize * (r.boss ? 2.2f : 1.8f), WithAlpha(EnemyGlow, r.boss ? 0.35f : 0.22f));
            if (r.flashBlindTimer > 0f || r.slowTimer > 0f)
                DrawCircleOutline(p, currSize + 14f, LightUi, 2f);
            if (r.longShotFlash > 0f)
                DrawCircleOutline(p, currSize + 22f, Gold, 3f);
            DrawCircle(p, currSize, r.boss ? Boss : new Color32(130, 10, 10, 255));
            DrawCircle(p, currSize * 0.6f, r.boss ? BossCore : new Color32(180, 30, 30, 255));
            DrawCircle(p + new Vector2(-8f, -8f), Mathf.Max(3f, currSize * 0.12f), r.boss ? new Color32(255, 130, 70, 255) : new Color32(255, 80, 80, 255));
            float barW = r.size * 1.5f;
            DrawRect(new Rect(p.x - barW / 2f, p.y - r.size - 20f, barW, 6f), new Color32(40, 0, 0, 255));
            DrawRect(new Rect(p.x - barW / 2f, p.y - r.size - 20f, barW * Mathf.Clamp01((float)r.hp / r.maxHp), 6f), new Color32(150, 0, 0, 255));
        }
    }

    void DrawBullets(Vector2 offset)
    {
        foreach (Bullet b in bullets)
        {
            Vector2 p = b.pos + offset;
            DrawLine(p - b.vel * 0.7f, p, new Color32(110, 90, 20, 255), 3f);
            DrawCircle(p, 6f, Color.gray);
            DrawCircle(p, 4f, Reload);
        }
        foreach (Bullet b in enemyBullets)
        {
            Vector2 p = b.pos + offset;
            Color core = b.longShot ? Gold : Color.red;
            if (b.homingActive) core = new Color32(255, 60, 210, 255);
            Color trailColor = b.longShot ? new Color32(230, 120, 20, 255) : Blood;
            DrawLine(p - b.vel * 0.7f, p, trailColor, 3f);
            DrawCircle(p, 6f, Color.gray);
            DrawCircle(p, 4f, core);
            if (b.homingActive) DrawCircleOutline(p, 12f, core, 1f);
        }
    }

    void DrawAim(Vector2 offset)
    {
        bool aiming = UseMobileControls() ? mobileAiming : Input.GetMouseButton(0);
        if (!aiming || state != GameState.Play || paused || isDying)
            return;

        Vector2 mouse = UseMobileControls() ? player + mobileAimDir * BulletMaxDist : MouseVirtual();
        float dist = Mathf.Min(Vector2.Distance(mouse, player), BulletMaxDist);
        float ang = Mathf.Atan2(mouse.y - player.y, mouse.x - player.x);
        Vector2 end = player + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
        if (GetAimRicochet(player, end, out Vector2 hit, out Vector2 reflect))
        {
            DrawLine(player + offset, hit + offset, new Color(1f, 1f, 1f, 0.3f), 16f);
            DrawLine(hit + offset, reflect + offset, new Color(1f, 1f, 1f, 0.3f), 10f);
            DrawCircle(hit + offset, 6f, new Color(1f, 1f, 1f, 0.47f));
        }
        else
        {
            DrawLine(player + offset, end + offset, new Color(1f, 1f, 1f, 0.3f), 16f);
            DrawCircle(end + offset, 8f, new Color(1f, 1f, 1f, 0.3f));
        }

        if (fCharging)
        {
            DrawCircleOutline(mouse + offset, 30f, Color.red, 2f);
            DrawLine(player + offset, mouse + offset, Color.red, 1f);
        }
    }

    bool GetAimRicochet(Vector2 start, Vector2 end, out Vector2 hitPoint, out Vector2 reflectEnd)
    {
        hitPoint = Vector2.zero;
        reflectEnd = Vector2.zero;
        Vector2 delta = end - start;
        float dist = delta.magnitude;
        if (dist <= 0.001f) return false;

        Rect hitWall = new Rect();
        bool hit = false;
        int steps = Mathf.RoundToInt(dist);
        for (int i = 0; i < steps; i += 2)
        {
            Vector2 p = start + delta * (i / dist);
            foreach (Rect w in walls)
            {
                if (w.Contains(p))
                {
                    hitPoint = p;
                    hitWall = w;
                    hit = true;
                    break;
                }
            }
            if (hit) break;
        }

        if (!hit) return false;
        Vector2 reflected = delta;
        if (hitPoint.x <= hitWall.xMin + 5f || hitPoint.x >= hitWall.xMax - 5f)
            reflected.x *= -1f;
        else
            reflected.y *= -1f;

        float remaining = BulletMaxDist * 1.5f - Vector2.Distance(hitPoint, start);
        reflectEnd = hitPoint + reflected.normalized * remaining;
        return true;
    }

    void DrawPlayer(Vector2 offset)
    {
        if (isDying) return;

        for (int i = 0; i < trail.Count; i++)
        {
            float k = (i + 1f) / Mathf.Max(1f, trail.Count);
            Color c = isDashing ? WithAlpha(DashGhost, k * 0.7f) : new Color(0.5f, 0.58f, 0.7f, k * 0.55f);
            DrawCircle(trail[i] + offset, k * (isDashing ? 25f : 18f), c);
        }

        Vector2 p = player + offset;
        if (shieldTimer > 0f) DrawCircleOutline(p, 28f, Dash, 1f);
        if (isDashing)
        {
            DrawCircleOutline(p, 34f, WithAlpha(DashGhost, 0.35f), 2f);
            DrawCircle(p, 22f, WithAlpha(DashGhost, 0.12f));
            DrawLine(p + new Vector2(-33f, 0f), p + new Vector2(33f, 0f), WithAlpha(DashGhost, 0.5f), 2f);
            DrawLine(p + new Vector2(0f, -33f), p + new Vector2(0f, 33f), WithAlpha(DashGhost, 0.5f), 2f);
            return;
        }

        DrawSoftCircle(p, 42f, WithAlpha(Dash, 0.16f));
        DrawCircle(p, 16f, new Color32(200, 210, 230, 255));
        DrawCircle(p + new Vector2(-4f, -4f), 4f, Color.white);
    }

    void DrawHud()
    {
        for (int i = 0; i < 3; i++)
        {
            Rect r = new Rect(40f + i * 65f, VH - 60f, 55f, 20f);
            DrawRect(r, i < hp ? new Color32(180, 0, 0, 255) : new Color32(40, 0, 0, 255));
            DrawRectOutline(r, Color.gray, 1f);
        }

        DrawText("AMMO: " + ammo + " | ROOM: " + rooms, new Rect(40f, 35f, 500f, 45f), 32, new Color32(200, 200, 200, 255), LeftStyle(32));
        DrawRect(new Rect(40f, VH - 100f, 180f, 12f), new Color32(60, 60, 60, 255));
        DrawRect(new Rect(42f, VH - 98f, 1.76f * light, 8f), LightUi);
        if (ultCooldown > 0f) DrawText("ULT: " + ultCooldown.ToString("0.0") + "s", new Rect(40f, 135f, 300f, 35f), 24, Color.red, LeftStyle(24));
        if (adrenalineTimer > 0f) DrawText("ADRENALINE: " + adrenalineTimer.ToString("0.0") + "s", new Rect(40f, 165f, 360f, 32f), 22, new Color32(255, 150, 30, 255), LeftStyle(22));
        if (isDashing) DrawText("INVISIBLE", new Rect(40f, 195f, 250f, 32f), 22, Color.cyan, LeftStyle(22));
        if (hp < 3) DrawText("REGEN: " + Mathf.CeilToInt(regenTimer) + "s", new Rect(40f, 225f, 250f, 32f), 22, Color.green, LeftStyle(22));
        if (bossRoomWarningTimer > 0f) DrawText("BOSS ARENA: NO INNER WALLS", new Rect(0f, 80f, VW, 40f), 28, Color.red, labelStyle);

        DrawRect(VolSlider, new Color32(40, 40, 40, 255));
        DrawRect(new Rect(VolSlider.x, VolSlider.y, VolSlider.width * musicVolume, VolSlider.height), new Color32(200, 0, 0, 255));
        Rect handle = new Rect(VolSlider.x + VolSlider.width * musicVolume - 10f, 30f, 20f, 30f);
        DrawRect(handle, new Color32(220, 220, 220, 255));
        DrawText("VOL: " + Mathf.RoundToInt(musicVolume * 100f) + "%", new Rect(VW - 430f, 30f, 160f, 32f), 20, Color.white, RightStyle(20));

        Vector2 mouse = MouseVirtual();
        if (Event.current.type == EventType.MouseDown && handle.Contains(mouse)) volumeDragging = true;
        if (Event.current.type == EventType.MouseUp) volumeDragging = false;
        if (volumeDragging)
        {
            float rel = Mathf.Clamp(mouse.x - VolSlider.x, 0f, VolSlider.width);
            musicVolume = rel / VolSlider.width;
        }
    }

    void DrawMobileControls()
    {
        if (!UseMobileControls() || state != GameState.Play || paused || isDying)
            return;

        Vector2 leftBase = leftTouchId == -1 ? new Vector2(190f, VH - 170f) : leftStickCenter;
        Vector2 leftKnob = leftTouchId == -1 ? leftBase : leftStickKnob;
        Vector2 rightBase = rightTouchId == -1 ? new Vector2(VW - 185f, VH - 170f) : rightStickCenter;
        Vector2 rightKnob = rightTouchId == -1 ? rightBase : rightStickKnob;

        DrawSoftCircle(leftBase, 118f, new Color(0f, 0.5f, 0.75f, 0.20f));
        DrawCircleOutline(leftBase, 112f, new Color(0.25f, 0.9f, 1f, 0.38f), 4f);
        DrawCircle(leftKnob, 48f, new Color(0.35f, 0.95f, 1f, 0.48f));
        DrawText("MOVE", new Rect(leftBase.x - 85f, leftBase.y + 80f, 170f, 30f), 20, new Color(0.7f, 1f, 1f, 0.75f), labelStyle);

        DrawSoftCircle(rightBase, 118f, new Color(0.65f, 0f, 0f, 0.20f));
        DrawCircleOutline(rightBase, 112f, new Color(1f, 0.3f, 0.2f, 0.42f), 4f);
        DrawCircle(rightKnob, 48f, new Color(1f, 0.18f, 0.12f, 0.52f));
        DrawText("AIM", new Rect(rightBase.x - 85f, rightBase.y + 80f, 170f, 30f), 20, new Color(1f, 0.75f, 0.7f, 0.75f), labelStyle);

        DrawMobileButton(MobileDashButton, "DASH", dashCooldown <= 0f, Dash);
        DrawMobileButton(MobileFlashButton, "FLASH", light >= 30f, LightUi);
        DrawMobileButton(MobileUltButton, "ULT", ultCooldown <= 0f, Gold);
    }

    void DrawMobileButton(Rect r, string text, bool ready, Color color)
    {
        Color fill = ready ? WithAlpha(color, 0.34f) : new Color(0.15f, 0.15f, 0.15f, 0.45f);
        Color edge = ready ? WithAlpha(color, 0.82f) : new Color(0.45f, 0.45f, 0.45f, 0.45f);
        DrawSoftCircle(r.center, Mathf.Max(r.width, r.height) * 0.62f, fill);
        DrawCircleOutline(r.center, Mathf.Max(r.width, r.height) * 0.5f, edge, 4f);
        DrawText(text, r, 18, ready ? Color.white : Color.gray, labelStyle);
    }

    void DrawButton(Rect rect, string text, bool danger)
    {
        Vector2 mouse = MouseVirtual();
        bool hover = rect.Contains(mouse);
        Color baseColor = danger
            ? (hover ? new Color32(95, 8, 8, 255) : new Color32(42, 5, 7, 255))
            : (hover ? new Color32(70, 12, 12, 255) : new Color32(32, 7, 9, 255));
        DrawRect(rect, baseColor);
        DrawRectOutline(rect, danger || hover ? UiEdge : new Color32(150, 0, 0, 255), 2f);
        DrawLine(new Vector2(rect.xMin + 18f, rect.yMin + 8f), new Vector2(rect.xMax - 18f, rect.yMin + 8f), new Color32(255, 90, 70, 255), 1f);
        DrawText(text, rect, 28, Color.white, labelStyle);
    }

    void DrawVignette()
    {
        DrawRect(new Rect(0f, 0f, VW, 45f), new Color(0f, 0f, 0f, 0.45f));
        DrawRect(new Rect(0f, VH - 45f, VW, 45f), new Color(0f, 0f, 0f, 0.45f));
        DrawRect(new Rect(0f, 0f, 45f, VH), new Color(0f, 0f, 0f, 0.45f));
        DrawRect(new Rect(VW - 45f, 0f, 45f, VH), new Color(0f, 0f, 0f, 0.45f));
    }

    void DrawText(string text, Rect rect, int size, Color color, GUIStyle style)
    {
        style.fontSize = size;
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }

    GUIStyle LeftStyle(int size)
    {
        var s = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft, fontSize = size };
        return s;
    }

    GUIStyle RightStyle(int size)
    {
        var s = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleRight, fontSize = size };
        return s;
    }

    void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, whiteTex);
        GUI.color = old;
    }

    void DrawCircle(Vector2 center, float radius, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), circleTex);
        GUI.color = old;
    }

    void DrawSoftCircle(Vector2 center, float radius, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), softCircleTex);
        GUI.color = old;
    }

    void DrawRectOutline(Rect r, Color c, float thickness)
    {
        DrawRect(new Rect(r.xMin, r.yMin, r.width, thickness), c);
        DrawRect(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), c);
        DrawRect(new Rect(r.xMin, r.yMin, thickness, r.height), c);
        DrawRect(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), c);
    }

    void DrawCircleOutline(Vector2 center, float radius, Color c, float thickness)
    {
        const int segments = 48;
        Vector2 last = center + new Vector2(radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector2 next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            DrawLine(last, next, c, thickness);
            last = next;
        }
    }

    void DrawLine(Vector2 a, Vector2 b, Color c, float thickness)
    {
        Vector2 d = b - a;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float len = d.magnitude;
        Color oldColor = GUI.color;
        Matrix4x4 old = GUI.matrix;
        GUI.color = c;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - thickness / 2f, len, thickness), whiteTex);
        GUI.matrix = old;
        GUI.color = oldColor;
    }

    static Color WithAlpha(Color c, float alpha)
    {
        c.a = alpha;
        return c;
    }

    static Rect Move(Rect r, Vector2 d)
    {
        return new Rect(r.x + d.x, r.y + d.y, r.width, r.height);
    }

    // Monetization hook:
    // One-file Unity code cannot show real Google Play ads or in-app purchases by itself.
    // Add the official SDK later (Google Mobile Ads, Unity LevelPlay, or Unity IAP),
    // then call this method after GameOver, between rooms, or from the main menu.
    public void ShowRewardedAdOrIapHook()
    {
        Debug.Log("Connect Google Play / ads SDK here.");
    }
}
