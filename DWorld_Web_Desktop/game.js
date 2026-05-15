(() => {
  const MOBILE = document.title.toLowerCase().includes("mobile");
  const canvas = document.getElementById("game");
  const ctx = canvas.getContext("2d");
  const menu = document.getElementById("menu");
  const pauseScreen = document.getElementById("pause");
  const gameOverScreen = document.getElementById("gameOver");
  const hud = document.getElementById("hud");
  const bestRoomEl = document.getElementById("bestRoom");
  const hpText = document.getElementById("hpText");
  const ammoText = document.getElementById("ammoText");
  const roomText = document.getElementById("roomText");
  const runSummary = document.getElementById("runSummary");

  const music = new Audio("assets/LOST.mp3");
  const deathSound = new Audio("assets/myinstants.mp3");
  music.loop = true;
  music.volume = 0.42;
  deathSound.volume = 0.72;

  const world = { w: 1600, h: 900 };
  const state = {
    mode: "menu",
    training: false,
    music: true,
    room: 1,
    best: Number(localStorage.getItem("dworldBestRoom") || "1"),
    time: 0,
    shake: 0,
    flash: 0,
    damageFlash: 0,
    pointerAim: { x: 1, y: 0 },
    mouse: { x: world.w / 2, y: world.h / 2, down: false },
    keys: new Set(),
    leftStick: null,
    rightStick: null,
    move: { x: 0, y: 0 },
    aimActive: false,
    paused: false
  };

  const player = {
    x: world.w / 2,
    y: world.h / 2,
    r: 18,
    hp: 3,
    maxHp: 3,
    ammo: 60,
    light: 100,
    invuln: 0,
    dash: 0,
    dashCd: 0,
    shootCd: 0,
    ultCd: 0,
    regen: 25,
    adrenaline: 0,
    deadTimer: 0
  };

  let dpr = 1;
  let scale = 1;
  let offsetX = 0;
  let offsetY = 0;
  let last = performance.now();
  let walls = [];
  let gates = [];
  let enemies = [];
  let bullets = [];
  let particles = [];
  let loot = [];

  bestRoomEl.textContent = state.best;

  function resize() {
    dpr = Math.min(2, window.devicePixelRatio || 1);
    canvas.width = Math.floor(innerWidth * dpr);
    canvas.height = Math.floor(innerHeight * dpr);
    canvas.style.width = `${innerWidth}px`;
    canvas.style.height = `${innerHeight}px`;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    scale = Math.min(innerWidth / world.w, innerHeight / world.h);
    offsetX = (innerWidth - world.w * scale) / 2;
    offsetY = (innerHeight - world.h * scale) / 2;
  }

  function screenToWorld(clientX, clientY) {
    return {
      x: (clientX - offsetX) / scale,
      y: (clientY - offsetY) / scale
    };
  }

  function requestFullScreen() {
    const el = document.documentElement;
    if (document.fullscreenElement) return;
    if (el.requestFullscreen) el.requestFullscreen().catch(() => {});
  }

  function startGame(training = false) {
    requestFullScreen();
    state.training = training;
    state.mode = "play";
    state.paused = false;
    hideAllScreens();
    hud.classList.remove("hidden");
    resetRun();
    if (state.music) music.play().catch(() => {});
  }

  function resetRun() {
    state.room = state.training ? 0 : 1;
    state.time = 0;
    state.shake = 0;
    state.flash = 0;
    player.x = world.w / 2;
    player.y = world.h / 2;
    player.hp = 3;
    player.ammo = state.training ? 999 : 60;
    player.light = 100;
    player.invuln = 1;
    player.dash = 0;
    player.dashCd = 0;
    player.shootCd = 0;
    player.ultCd = 0;
    player.regen = 25;
    player.adrenaline = 0;
    player.deadTimer = 0;
    walls = [];
    gates = [];
    enemies = [];
    bullets = [];
    particles = [];
    loot = [];
    generateRoom();
  }

  function generateRoom() {
    walls = [];
    gates = [];
    bullets = bullets.filter(b => b.friendly);
    enemies = [];
    loot = [];
    const t = 45;
    const h = 210;
    walls.push({ x: 0, y: 0, w: world.w / 2 - h, h: t });
    walls.push({ x: world.w / 2 + h, y: 0, w: world.w / 2 - h, h: t });
    walls.push({ x: 0, y: world.h - t, w: world.w / 2 - h, h: t });
    walls.push({ x: world.w / 2 + h, y: world.h - t, w: world.w / 2 - h, h: t });
    walls.push({ x: 0, y: 0, w: t, h: world.h / 2 - h });
    walls.push({ x: 0, y: world.h / 2 + h, w: t, h: world.h / 2 - h });
    walls.push({ x: world.w - t, y: 0, w: t, h: world.h / 2 - h });
    walls.push({ x: world.w - t, y: world.h / 2 + h, w: t, h: world.h / 2 - h });
    gates.push({ x: world.w / 2 - h, y: 0, w: h * 2, h: t });
    gates.push({ x: world.w / 2 - h, y: world.h - t, w: h * 2, h: t });
    gates.push({ x: 0, y: world.h / 2 - h, w: t, h: h * 2 });
    gates.push({ x: world.w - t, y: world.h / 2 - h, w: t, h: h * 2 });

    const bossRoom = state.training || state.room % 5 === 0;
    if (!bossRoom) {
      for (let i = 0; i < 12; i++) {
        const w = { x: rand(240, world.w - 340), y: rand(190, world.h - 290), w: rand(70, 130), h: rand(70, 130) };
        const tooClose = rectCircle(w, world.w / 2, world.h / 2, 180) || walls.some(o => rectOverlap(inflate(w, 170), o));
        if (!tooClose) walls.push(w);
      }
    }

    if (bossRoom) {
      spawnEnemy(world.w - 230, world.h / 2, true);
      if (state.training) {
        enemies[0].hp = 420;
        enemies[0].maxHp = 420;
        enemies[0].name = "THE TRAINING TITAN";
      }
    } else {
      const count = Math.min(12, 5 + Math.floor(state.room / 2));
      for (let i = 0; i < count; i++) {
        spawnEnemy(Math.random() < 0.5 ? 150 : world.w - 150, Math.random() < 0.5 ? 150 : world.h - 150, false);
      }
    }

    player.x = world.w / 2;
    player.y = world.h / 2;
    player.invuln = 1.1;
    burst(player.x, player.y, "#56f5ff", 50, 7);
  }

  function spawnEnemy(x, y, boss) {
    const level = Math.max(1, state.room);
    const hp = boss ? 45 + level * 14 : 4 + Math.floor(level / 2);
    enemies.push({
      x, y, boss,
      name: boss ? "VOID BRUTE" : "hunter",
      r: boss ? 72 : 24,
      hp,
      maxHp: hp,
      speed: boss ? 70 + level * 4 : rand(110, 145) + level * 4,
      shoot: rand(0.4, 2.1),
      slow: 0,
      flash: 0,
      strafe: Math.random() < 0.5 ? -1 : 1,
      ideal: boss ? 220 : rand(230, 470),
      phase: Math.random() * Math.PI * 2
    });
  }

  function update(dt) {
    state.time += dt;
    if (state.mode !== "play" || state.paused) return;
    if (player.deadTimer > 0) {
      player.deadTimer -= dt;
      burst(player.x, player.y, "#ff1d25", 8, 12);
      if (player.deadTimer <= 0) showGameOver();
      return;
    }

    state.shake *= Math.pow(0.88, dt * 60);
    state.flash = Math.max(0, state.flash - dt * 3.8);
    state.damageFlash = Math.max(0, state.damageFlash - dt * 3.4);
    player.invuln = Math.max(0, player.invuln - dt);
    player.dashCd = Math.max(0, player.dashCd - dt);
    player.shootCd = Math.max(0, player.shootCd - dt);
    player.ultCd = Math.max(0, player.ultCd - dt);
    player.adrenaline = Math.max(0, player.adrenaline - dt);
    player.light = Math.min(100, player.light + dt * 3.8);

    if (player.hp < player.maxHp) {
      player.regen -= dt;
      if (player.regen <= 0) {
        player.hp++;
        player.regen = 25;
        burst(player.x, player.y, "#44ff89", 42, 7);
      }
    } else {
      player.regen = 25;
    }

    movePlayer(dt);
    updateEnemies(dt);
    updateBullets(dt);
    updateLoot();
    updateParticles(dt);

    if (!state.training && enemies.length === 0 && (player.x < 0 || player.x > world.w || player.y < 0 || player.y > world.h)) {
      state.room++;
      state.best = Math.max(state.best, state.room);
      localStorage.setItem("dworldBestRoom", String(state.best));
      bestRoomEl.textContent = state.best;
      generateRoom();
    }

    hpText.textContent = `HP ${player.hp}`;
    ammoText.textContent = `AMMO ${player.ammo}`;
    roomText.textContent = state.training ? "TRAINING" : `ROOM ${state.room}`;
  }

  function movePlayer(dt) {
    let mx = 0, my = 0;
    if (state.keys.has("KeyA")) mx--;
    if (state.keys.has("KeyD")) mx++;
    if (state.keys.has("KeyW")) my--;
    if (state.keys.has("KeyS")) my++;
    if (MOBILE || state.leftStick) {
      mx = state.move.x;
      my = state.move.y;
    }
    const len = Math.hypot(mx, my) || 1;
    mx /= len;
    my /= len;
    let speed = player.dash > 0 ? 740 : 245;
    if (player.adrenaline > 0) speed *= 1.22;
    if (player.dash > 0) player.dash -= dt;
    axisMove(mx * speed * dt, 0);
    axisMove(0, my * speed * dt);
    if (Math.abs(mx) + Math.abs(my) > 0.05) {
      particles.push({ x: player.x, y: player.y, vx: rand(-25, 25), vy: rand(-25, 25), life: 0.45, max: 0.45, r: rand(2, 5), c: "rgba(85,245,255,.5)" });
    }
  }

  function axisMove(dx, dy) {
    const oldX = player.x, oldY = player.y;
    player.x += dx;
    player.y += dy;
    const blockers = enemies.length ? walls.concat(gates) : walls;
    if (blockers.some(w => rectCircle(w, player.x, player.y, player.r))) {
      player.x = oldX;
      player.y = oldY;
    }
  }

  function updateEnemies(dt) {
    for (const e of enemies) {
      e.slow = Math.max(0, e.slow - dt);
      e.flash = Math.max(0, e.flash - dt);
      const dx = player.x - e.x;
      const dy = player.y - e.y;
      const dist = Math.hypot(dx, dy) || 1;
      const ang = Math.atan2(dy, dx);
      let moveAng = dist < e.ideal ? ang + e.strafe * 1.35 : ang + e.strafe * 0.34;
      let speed = e.speed * (e.slow > 0 ? 0.35 : 1);
      const oldX = e.x, oldY = e.y;
      e.x += Math.cos(moveAng) * speed * dt;
      e.y += Math.sin(moveAng) * speed * dt;
      if (walls.concat(gates).some(w => rectCircle(w, e.x, e.y, e.r))) {
        e.x = oldX;
        e.y = oldY;
        e.strafe *= -1;
      }
      e.shoot -= dt;
      if (e.shoot <= 0 && player.dash <= 0 && e.flash <= 0) {
        enemyShot(e, ang);
        e.shoot = e.boss ? rand(0.35, 0.95) : rand(0.75, 1.8);
      }
      if (dist < e.r + player.r && player.invuln <= 0 && player.dash <= 0) damagePlayer();
    }
  }

  function enemyShot(e, ang) {
    const boss = e.boss;
    const longShot = boss && Math.random() < 0.23;
    const speed = longShot ? 520 : boss ? 440 : 370;
    const spread = boss ? 0.1 : 0.05;
    const count = boss && Math.random() < 0.32 ? 3 : 1;
    for (let i = 0; i < count; i++) {
      const a = ang + (i - (count - 1) / 2) * 0.22 + rand(-spread, spread);
      bullets.push({
        x: e.x, y: e.y,
        vx: Math.cos(a) * speed,
        vy: Math.sin(a) * speed,
        r: boss ? 7 : 5,
        life: longShot ? 3.2 : 2.1,
        friendly: false,
        homing: Math.random() < (boss ? 0.38 : 0.16),
        a,
        speed,
        c: longShot ? "#ffd35b" : boss ? "#ff375a" : "#ff1d25"
      });
    }
  }

  function updateBullets(dt) {
    for (let i = bullets.length - 1; i >= 0; i--) {
      const b = bullets[i];
      if (b.homing && !b.friendly && player.dash <= 0) {
        const ta = Math.atan2(player.y - b.y, player.x - b.x);
        const diff = angleDiff(ta, b.a);
        if (Math.abs(diff) < Math.PI / 4) {
          b.a += clamp(diff, -3.8 * dt, 3.8 * dt);
          b.vx = Math.cos(b.a) * b.speed;
          b.vy = Math.sin(b.a) * b.speed;
        } else {
          b.homing = false;
        }
      }

      const ox = b.x, oy = b.y;
      b.x += b.vx * dt;
      b.y += b.vy * dt;
      b.life -= dt;
      particles.push({ x: b.x, y: b.y, vx: 0, vy: 0, life: 0.16, max: 0.16, r: b.r * 1.3, c: b.friendly ? "rgba(255,220,90,.45)" : "rgba(255,40,70,.45)" });

      const hitWall = walls.find(w => pointRect(b.x, b.y, w));
      if (hitWall) {
        if (b.friendly && !b.bounced) {
          b.bounced = true;
          b.life += 0.75;
          if (ox < hitWall.x || ox > hitWall.x + hitWall.w) b.vx *= -1;
          else b.vy *= -1;
          b.x = ox;
          b.y = oy;
          burst(b.x, b.y, "#c6d0d8", 8, 4);
        } else {
          burst(b.x, b.y, b.c, 10, 4);
          bullets.splice(i, 1);
          continue;
        }
      }

      if (b.life <= 0) {
        burst(b.x, b.y, b.c, 8, 5);
        bullets.splice(i, 1);
        continue;
      }

      if (b.friendly) {
        for (let j = enemies.length - 1; j >= 0; j--) {
          const e = enemies[j];
          if (Math.hypot(b.x - e.x, b.y - e.y) < b.r + e.r) {
            e.hp -= b.damage || 1;
            burst(b.x, b.y, "#ff2634", 14, 6);
            bullets.splice(i, 1);
            if (e.hp <= 0) killEnemy(j);
            break;
          }
        }
      } else if (Math.hypot(b.x - player.x, b.y - player.y) < b.r + player.r && player.invuln <= 0 && player.dash <= 0) {
        bullets.splice(i, 1);
        damagePlayer();
      }
    }
  }

  function killEnemy(index) {
    const e = enemies[index];
    burst(e.x, e.y, e.boss ? "#ff7a2d" : "#ff2634", e.boss ? 90 : 32, e.boss ? 13 : 8);
    if (!state.training) loot.push({ x: e.x, y: e.y, type: Math.random() < 0.22 ? "hp" : "ammo" });
    enemies.splice(index, 1);
    if (state.training && enemies.length === 0) setTimeout(() => spawnEnemy(world.w - 230, world.h / 2, true), 950);
  }

  function updateLoot() {
    for (let i = loot.length - 1; i >= 0; i--) {
      const l = loot[i];
      if (Math.hypot(l.x - player.x, l.y - player.y) < 48) {
        if (l.type === "hp") player.hp = Math.min(player.maxHp, player.hp + 1);
        else player.ammo += 18;
        burst(l.x, l.y, l.type === "hp" ? "#47ff8f" : "#ffd65b", 22, 7);
        loot.splice(i, 1);
      }
    }
  }

  function updateParticles(dt) {
    for (let i = particles.length - 1; i >= 0; i--) {
      const p = particles[i];
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.life -= dt;
      if (p.life <= 0) particles.splice(i, 1);
    }
  }

  function damagePlayer() {
    player.hp--;
    player.invuln = 1.05;
    state.damageFlash = 1;
    state.shake = 16;
    burst(player.x, player.y, "#ff1d25", 36, 10);
    if (player.hp === 1) player.adrenaline = 5;
    if (player.hp <= 0) {
      player.deadTimer = 1.45;
      music.pause();
      deathSound.currentTime = 0;
      deathSound.play().catch(() => {});
    }
  }

  function shoot(angle, power = 1) {
    if (player.shootCd > 0 || player.ammo <= 0 || player.deadTimer > 0) return;
    player.ammo--;
    player.shootCd = MOBILE ? 0.18 : 0.22;
    state.shake = 4;
    bullets.push({
      x: player.x,
      y: player.y,
      vx: Math.cos(angle) * 760 * power,
      vy: Math.sin(angle) * 760 * power,
      r: 6,
      life: 0.62,
      friendly: true,
      bounced: false,
      damage: 1,
      c: "#ffd65b"
    });
  }

  function dash() {
    if (player.dashCd > 0) return;
    player.dash = 0.18;
    player.dashCd = 1.35;
    player.invuln = Math.max(player.invuln, 0.18);
    burst(player.x, player.y, "#55f5ff", 30, 10);
  }

  function flash() {
    if (player.light < 30) return;
    player.light -= 30;
    state.flash = 1;
    for (const e of enemies) {
      if (Math.hypot(e.x - player.x, e.y - player.y) < 530) {
        e.slow = 4;
        e.flash = 4;
        burst(e.x, e.y, "#f4eaa0", 28, 8);
      }
    }
  }

  function ult() {
    if (player.ultCd > 0) return;
    player.ultCd = player.adrenaline > 0 ? 4.5 : 9.5;
    state.shake = 25;
    for (let i = 0; i < 28; i++) shootFree(Math.PI * 2 * i / 28, 1.05);
  }

  function shootFree(angle, power = 1) {
    bullets.push({
      x: player.x, y: player.y,
      vx: Math.cos(angle) * 760 * power,
      vy: Math.sin(angle) * 760 * power,
      r: 6, life: 0.7,
      friendly: true,
      bounced: false,
      damage: 1,
      c: "#ffd65b"
    });
  }

  function draw() {
    ctx.save();
    ctx.clearRect(0, 0, innerWidth, innerHeight);
    ctx.translate(offsetX, offsetY);
    ctx.scale(scale, scale);
    const sx = rand(-state.shake, state.shake);
    const sy = rand(-state.shake, state.shake);
    ctx.translate(sx, sy);

    drawBackdrop();
    drawArena();
    drawLoot();
    drawEnemies();
    drawBullets();
    drawPlayer();
    drawTouchControls();
    drawOverlays();

    ctx.restore();
  }

  function drawBackdrop() {
    const g = ctx.createRadialGradient(world.w / 2, world.h / 2, 10, world.w / 2, world.h / 2, 850);
    g.addColorStop(0, "#13131c");
    g.addColorStop(0.55, "#07060d");
    g.addColorStop(1, "#020106");
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, world.w, world.h);
    ctx.strokeStyle = "rgba(70,220,255,.09)";
    ctx.lineWidth = 1;
    const pulse = Math.sin(state.time * 2) * 7;
    for (let x = 0; x <= world.w; x += 80) line(x + pulse, 0, x + pulse, world.h);
    for (let y = 0; y <= world.h; y += 80) line(0, y - pulse, world.w, y - pulse);
    ctx.strokeStyle = "rgba(255,36,48,.12)";
    ctx.lineWidth = 2;
    circle(world.w / 2, world.h / 2, 260 + Math.sin(state.time * 2) * 8, false);
    circle(world.w / 2, world.h / 2, 430, false);
  }

  function drawArena() {
    for (const w of walls) {
      ctx.fillStyle = "#151114";
      ctx.shadowColor = "#ff2634";
      ctx.shadowBlur = 10;
      ctx.fillRect(w.x, w.y, w.w, w.h);
      ctx.shadowBlur = 0;
      ctx.strokeStyle = "rgba(255,90,80,.45)";
      ctx.lineWidth = 2;
      ctx.strokeRect(w.x + 1, w.y + 1, w.w - 2, w.h - 2);
    }
    const gateColor = enemies.length ? "rgba(255,40,50,.8)" : "rgba(65,255,130,.8)";
    ctx.strokeStyle = gateColor;
    ctx.lineWidth = 4;
    for (const g of gates) ctx.strokeRect(g.x, g.y, g.w, g.h);
  }

  function drawLoot() {
    for (const l of loot) {
      const c = l.type === "hp" ? "#47ff8f" : "#ffd65b";
      glow(l.x, l.y + Math.sin(state.time * 8) * 5, 28, c, 0.35);
      ctx.fillStyle = c;
      ctx.fillRect(l.x - 10, l.y - 10, 20, 20);
    }
  }

  function drawEnemies() {
    for (const e of enemies) {
      const pulse = Math.sin(state.time * 5 + e.phase) * 3;
      glow(e.x, e.y, e.r * (e.boss ? 2.5 : 2), e.boss ? "#ff642b" : "#ff2634", e.boss ? 0.42 : 0.28);
      if (e.flash > 0) {
        ctx.strokeStyle = "rgba(255,245,160,.8)";
        ctx.lineWidth = 4;
        circle(e.x, e.y, e.r + 18, false);
      }
      ctx.fillStyle = e.boss ? "#ce101d" : "#8f0c14";
      circle(e.x, e.y, e.r + pulse, true);
      ctx.fillStyle = e.boss ? "#ff7a2d" : "#f03b45";
      circle(e.x, e.y, e.r * 0.56, true);
      ctx.fillStyle = "#fff4d6";
      circle(e.x - e.r * 0.22, e.y - e.r * 0.24, Math.max(3, e.r * 0.12), true);
      ctx.fillStyle = "rgba(40,0,0,.9)";
      ctx.fillRect(e.x - e.r, e.y - e.r - 24, e.r * 2, 7);
      ctx.fillStyle = e.boss ? "#ffb45a" : "#ff2b35";
      ctx.fillRect(e.x - e.r, e.y - e.r - 24, e.r * 2 * Math.max(0, e.hp / e.maxHp), 7);
      if (e.boss) {
        ctx.fillStyle = "rgba(255,255,255,.8)";
        ctx.font = "bold 18px system-ui";
        ctx.textAlign = "center";
        ctx.fillText(e.name, e.x, e.y - e.r - 38);
      }
    }
  }

  function drawBullets() {
    for (const b of bullets) {
      ctx.strokeStyle = b.friendly ? "rgba(255,210,70,.65)" : b.c;
      ctx.lineWidth = b.friendly ? 4 : 3;
      line(b.x - b.vx * 0.035, b.y - b.vy * 0.035, b.x, b.y);
      glow(b.x, b.y, b.r * 3.4, b.c, 0.22);
      ctx.fillStyle = b.c;
      circle(b.x, b.y, b.r, true);
      if (b.homing) {
        ctx.strokeStyle = "rgba(255,70,215,.65)";
        circle(b.x, b.y, b.r + 10, false);
      }
    }
    for (const p of particles) {
      ctx.globalAlpha = Math.max(0, p.life / p.max);
      ctx.fillStyle = p.c;
      circle(p.x, p.y, p.r * (p.life / p.max), true);
      ctx.globalAlpha = 1;
    }
  }

  function drawPlayer() {
    if (player.deadTimer > 0) return;
    glow(player.x, player.y, player.dash > 0 ? 72 : 46, "#55f5ff", player.dash > 0 ? 0.42 : 0.25);
    if (player.invuln > 0) {
      ctx.strokeStyle = "rgba(85,245,255,.75)";
      ctx.lineWidth = 3;
      circle(player.x, player.y, 34, false);
    }
    ctx.fillStyle = player.dash > 0 ? "#55f5ff" : "#dce8f7";
    circle(player.x, player.y, player.r, true);
    ctx.fillStyle = "#fff";
    circle(player.x - 5, player.y - 5, 4, true);
    if (state.aimActive || state.mouse.down) {
      const a = Math.atan2(state.pointerAim.y, state.pointerAim.x);
      const ex = player.x + Math.cos(a) * 450;
      const ey = player.y + Math.sin(a) * 450;
      ctx.strokeStyle = "rgba(255,255,255,.28)";
      ctx.lineWidth = 14;
      line(player.x, player.y, ex, ey);
    }
  }

  function drawTouchControls() {
    if (!MOBILE || state.mode !== "play" || state.paused) return;
    drawStick(state.leftStick, 190, world.h - 170, "#55f5ff", "MOVE");
    drawStick(state.rightStick, world.w - 190, world.h - 170, "#ff3442", "AIM");
    drawAbility(world.w - 365, world.h - 210, 45, "DASH", player.dashCd <= 0, "#55f5ff");
    drawAbility(world.w - 265, world.h - 292, 45, "FLASH", player.light >= 30, "#f4eaa0");
    drawAbility(world.w - 150, world.h - 210, 54, "ULT", player.ultCd <= 0, "#ffd65b");
  }

  function drawStick(stick, dx, dy, color, text) {
    const base = stick ? stick.base : { x: dx, y: dy };
    const knob = stick ? stick.knob : base;
    glow(base.x, base.y, 116, color, 0.14);
    ctx.strokeStyle = hexAlpha(color, 0.48);
    ctx.lineWidth = 4;
    circle(base.x, base.y, 108, false);
    ctx.fillStyle = hexAlpha(color, 0.42);
    circle(knob.x, knob.y, 48, true);
    ctx.fillStyle = "rgba(255,255,255,.76)";
    ctx.font = "bold 20px system-ui";
    ctx.textAlign = "center";
    ctx.fillText(text, base.x, base.y + 88);
  }

  function drawAbility(x, y, r, text, ready, color) {
    glow(x, y, r + 12, ready ? color : "#777", ready ? 0.28 : 0.12);
    ctx.strokeStyle = ready ? hexAlpha(color, 0.8) : "rgba(180,180,180,.42)";
    ctx.lineWidth = 4;
    circle(x, y, r, false);
    ctx.fillStyle = ready ? "rgba(255,255,255,.94)" : "rgba(180,180,180,.62)";
    ctx.font = "bold 18px system-ui";
    ctx.textAlign = "center";
    ctx.fillText(text, x, y + 6);
  }

  function drawOverlays() {
    if (state.flash > 0) {
      ctx.fillStyle = `rgba(255,255,255,${state.flash * 0.25})`;
      ctx.fillRect(0, 0, world.w, world.h);
    }
    if (state.damageFlash > 0) {
      ctx.fillStyle = `rgba(255,0,25,${state.damageFlash * 0.24})`;
      ctx.fillRect(0, 0, world.w, world.h);
    }
    const vg = ctx.createRadialGradient(world.w / 2, world.h / 2, 280, world.w / 2, world.h / 2, 900);
    vg.addColorStop(0, "rgba(0,0,0,0)");
    vg.addColorStop(1, "rgba(0,0,0,.66)");
    ctx.fillStyle = vg;
    ctx.fillRect(0, 0, world.w, world.h);
  }

  function showGameOver() {
    state.mode = "gameover";
    hud.classList.add("hidden");
    gameOverScreen.classList.remove("hidden");
    runSummary.textContent = state.training ? "Training complete. The boss will be waiting." : `You reached room ${state.room}. Best room ${state.best}.`;
  }

  function hideAllScreens() {
    menu.classList.add("hidden");
    pauseScreen.classList.add("hidden");
    gameOverScreen.classList.add("hidden");
  }

  function showMenu() {
    state.mode = "menu";
    state.paused = false;
    hud.classList.add("hidden");
    pauseScreen.classList.add("hidden");
    gameOverScreen.classList.add("hidden");
    menu.classList.remove("hidden");
    music.pause();
  }

  function pauseGame() {
    if (state.mode !== "play") return;
    state.paused = true;
    pauseScreen.classList.remove("hidden");
    hud.classList.add("hidden");
  }

  function resumeGame() {
    state.paused = false;
    pauseScreen.classList.add("hidden");
    hud.classList.remove("hidden");
    if (state.music) music.play().catch(() => {});
  }

  function pointerDown(e) {
    const p = screenToWorld(e.clientX, e.clientY);
    if (MOBILE) {
      if (p.x > world.w - 430 && p.y > world.h - 350) {
        if (Math.hypot(p.x - (world.w - 365), p.y - (world.h - 210)) < 62) dash();
        else if (Math.hypot(p.x - (world.w - 265), p.y - (world.h - 292)) < 62) flash();
        else if (Math.hypot(p.x - (world.w - 150), p.y - (world.h - 210)) < 70) ult();
        return;
      }
      if (p.x < world.w * 0.48 && !state.leftStick) {
        state.leftStick = { id: e.pointerId, base: p, knob: p };
      } else if (!state.rightStick) {
        state.rightStick = { id: e.pointerId, base: p, knob: p };
        state.aimActive = true;
      }
    } else {
      state.mouse.down = true;
      state.mouse = { ...state.mouse, ...p, down: true };
    }
  }

  function pointerMove(e) {
    const p = screenToWorld(e.clientX, e.clientY);
    state.mouse = { ...state.mouse, ...p };
    if (MOBILE && state.leftStick?.id === e.pointerId) {
      const dx = p.x - state.leftStick.base.x;
      const dy = p.y - state.leftStick.base.y;
      const len = Math.hypot(dx, dy) || 1;
      const mag = Math.min(1, len / 95);
      state.move = { x: dx / len * mag, y: dy / len * mag };
      state.leftStick.knob = { x: state.leftStick.base.x + dx / len * mag * 95, y: state.leftStick.base.y + dy / len * mag * 95 };
    }
    if (MOBILE && state.rightStick?.id === e.pointerId) {
      const dx = p.x - state.rightStick.base.x;
      const dy = p.y - state.rightStick.base.y;
      const len = Math.hypot(dx, dy) || 1;
      if (len > 18) state.pointerAim = { x: dx / len, y: dy / len };
      const mag = Math.min(1, len / 100);
      state.rightStick.knob = { x: state.rightStick.base.x + dx / len * mag * 100, y: state.rightStick.base.y + dy / len * mag * 100 };
    }
    if (!MOBILE) {
      const dx = p.x - player.x;
      const dy = p.y - player.y;
      const len = Math.hypot(dx, dy) || 1;
      state.pointerAim = { x: dx / len, y: dy / len };
    }
  }

  function pointerUp(e) {
    if (MOBILE && state.leftStick?.id === e.pointerId) {
      state.leftStick = null;
      state.move = { x: 0, y: 0 };
    }
    if (MOBILE && state.rightStick?.id === e.pointerId) {
      const a = Math.atan2(state.pointerAim.y, state.pointerAim.x);
      shoot(a);
      state.rightStick = null;
      state.aimActive = false;
    }
    if (!MOBILE && state.mouse.down) {
      state.mouse.down = false;
      shoot(Math.atan2(state.pointerAim.y, state.pointerAim.x));
    }
  }

  function burst(x, y, color, count, speed) {
    for (let i = 0; i < count; i++) {
      const a = Math.random() * Math.PI * 2;
      const s = rand(30, speed * 42);
      particles.push({ x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s, life: rand(0.25, 0.75), max: 0.75, r: rand(2, 6), c: color });
    }
  }

  function glow(x, y, r, color, alpha) {
    const g = ctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, hexAlpha(color, alpha));
    g.addColorStop(1, hexAlpha(color, 0));
    ctx.fillStyle = g;
    circle(x, y, r, true);
  }

  function circle(x, y, r, fill) {
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    fill ? ctx.fill() : ctx.stroke();
  }

  function line(x1, y1, x2, y2) {
    ctx.beginPath();
    ctx.moveTo(x1, y1);
    ctx.lineTo(x2, y2);
    ctx.stroke();
  }

  function rand(a, b) { return a + Math.random() * (b - a); }
  function clamp(v, a, b) { return Math.max(a, Math.min(b, v)); }
  function pointRect(x, y, r) { return x >= r.x && x <= r.x + r.w && y >= r.y && y <= r.y + r.h; }
  function rectOverlap(a, b) { return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y; }
  function inflate(r, n) { return { x: r.x - n / 2, y: r.y - n / 2, w: r.w + n, h: r.h + n }; }
  function rectCircle(r, x, y, radius) {
    const cx = clamp(x, r.x, r.x + r.w);
    const cy = clamp(y, r.y, r.y + r.h);
    return Math.hypot(x - cx, y - cy) < radius;
  }
  function angleDiff(a, b) { return ((a - b + Math.PI * 3) % (Math.PI * 2)) - Math.PI; }
  function hexAlpha(color, alpha) {
    if (color.startsWith("rgba")) return color;
    if (color.startsWith("#")) {
      const v = parseInt(color.slice(1), 16);
      return `rgba(${(v >> 16) & 255},${(v >> 8) & 255},${v & 255},${alpha})`;
    }
    return color;
  }

  function loop(now) {
    const dt = Math.min(0.033, (now - last) / 1000);
    last = now;
    update(dt);
    draw();
    requestAnimationFrame(loop);
  }

  addEventListener("resize", resize);
  addEventListener("keydown", e => {
    state.keys.add(e.code);
    if (e.code === "Escape") state.paused ? resumeGame() : pauseGame();
    if (state.mode === "play" && !state.paused) {
      if (e.code === "ShiftLeft" || e.code === "ControlLeft") dash();
      if (e.code === "KeyR") flash();
      if (e.code === "Space") ult();
    }
  });
  addEventListener("keyup", e => state.keys.delete(e.code));
  canvas.addEventListener("pointerdown", pointerDown);
  canvas.addEventListener("pointermove", pointerMove);
  canvas.addEventListener("pointerup", pointerUp);
  canvas.addEventListener("pointercancel", pointerUp);

  document.getElementById("playCampaign").onclick = () => startGame(false);
  document.getElementById("playTraining").onclick = () => startGame(true);
  document.getElementById("fullScreenBtn").onclick = requestFullScreen;
  document.getElementById("musicBtn").onclick = (e) => {
    state.music = !state.music;
    e.currentTarget.textContent = `Music: ${state.music ? "On" : "Off"}`;
    if (!state.music) music.pause();
    else if (state.mode === "play") music.play().catch(() => {});
  };
  document.getElementById("pauseBtn").onclick = pauseGame;
  document.getElementById("resumeBtn").onclick = resumeGame;
  document.getElementById("pauseMenuBtn").onclick = showMenu;
  document.getElementById("retryBtn").onclick = () => startGame(state.training);
  document.getElementById("deathMenuBtn").onclick = showMenu;

  resize();
  requestAnimationFrame(loop);
})();
