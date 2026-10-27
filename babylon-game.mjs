import { FightingEngine } from './fighting-engine.mjs';
import { findContact } from './contact.mjs';

const B = window.BABYLON;
const status = document.getElementById('status');
if (!B) throw new Error('Babylon.js failed to load');
const canvas = document.getElementById('game');
const engine = new B.Engine(canvas, true, { preserveDrawingBuffer: false, stencil: true });
const scene = new B.Scene(engine);
scene.clearColor = B.Color4.FromHexString('#1a222cff');
const camera = new B.FreeCamera('camera', new B.Vector3(0, 2.4, -12), scene);
camera.setTarget(new B.Vector3(0, 1.2, 0));
camera.mode = B.Camera.ORTHOGRAPHIC_CAMERA;
function resize() {
  const aspect = engine.getRenderWidth() / engine.getRenderHeight();
  const h = Math.max(3.2, 5.2 / aspect);
  camera.orthoLeft = -h * aspect; camera.orthoRight = h * aspect;
  camera.orthoTop = h; camera.orthoBottom = -h;
}
new B.HemisphericLight('sky', new B.Vector3(0, 1, 0), scene).intensity = 1.1;
const sun = new B.DirectionalLight('sun', new B.Vector3(-1, -2, -1), scene); sun.intensity = 1.2;
function mat(name, hex) { const m = new B.StandardMaterial(name, scene); m.diffuseColor = B.Color3.FromHexString(hex); return m; }
const ground = B.MeshBuilder.CreateGround('ground', { width: 30, height: 12 }, scene); ground.material = mat('floor', '#39434b');
const wall = B.MeshBuilder.CreatePlane('wall', { width: 30, height: 9 }, scene); wall.position.set(0, 4.5, 3); wall.material = mat('wallmat', '#263b4c'); wall.material.backFaceCulling = false;
for (let x = -12; x <= 12; x += 2) { const p = B.MeshBuilder.CreateBox('pillar', { width: 0.15, height: 5, depth: 0.12 }, scene); p.position.set(x, 2.5, 2.9); p.material = mat('pillar'+x, '#526579'); }
const girlMoves = { idle:['capoeira'], walk:['ginga_forward'], jab:['bencao'], heavy:['chapa_2','pontera'], kick:['armada','chapa_giratoria','meia_lua_de_frente','martelo_2'], dodge:['esquiva_1'] };
const kungMoves = { idle:['kung fu idle guard','kung fu idle guard2'], walk:['kung fu idle guard'], jab:['kung fu sexy punch','kung fu phantom punch'], heavy:['kung fu double push punch'], kick:['kung fu sexy kick'], dodge:['kung fu the misleader'] };
const defs = [
  {name:'Anime Girl', folder:'sketchgirl', scale:10, moves:girlMoves, hips:'mixamorig:Hips', spine:'mixamorig:Spine', head:'mixamorig:Head', leftFoot:'mixamorig:LeftFoot', rightFoot:'mixamorig:RightFoot', leftHand:'mixamorig:LeftHand', rightHand:'mixamorig:RightHand'},
  {name:'Kung Fu Fighter', folder:'kungfu', scale:0.62, moves:kungMoves, hips:'CC_Base_Hip_02', spine:'CC_Base_Spine02_035', head:'CC_Base_Head_038', leftFoot:'CC_Base_L_Foot_06', rightFoot:'CC_Base_R_Foot_021', leftHand:'CC_Base_L_Hand_055', rightHand:'CC_Base_R_Hand_083'}
];
const fighters = [];
let match;
let impactPauseUntil = 0;
const sparks = [];
const sparkMaterial = new B.StandardMaterial('impact', scene);
sparkMaterial.emissiveColor = B.Color3.FromHexString('#ffe17a');
function showImpact(point, now) {
  const mesh = B.MeshBuilder.CreateSphere('impact', { diameter: .22, segments: 6 }, scene);
  mesh.position.copyFrom(point); mesh.material = sparkMaterial;
  sparks.push({mesh, until:now+.15});
  impactPauseUntil = now+.085;
  fighters.forEach(f=>{ if(f.busyUntil>now)f.busyUntil+=.085; });
}
function pick(arr) { return arr[Math.floor(Math.random() * arr.length)]; }
async function loadFighter(spec, index) {
  const result = await B.SceneLoader.ImportMeshAsync(null, `models/${spec.folder}/`, 'character.glb', scene);
  const root = new B.TransformNode(spec.folder+'-fighter', scene);
  const pose = new B.TransformNode(spec.folder+'-pose', scene); pose.parent = root;
  const model = result.meshes[0]; model.parent = pose; model.scaling.scaleInPlace(spec.scale);
  const nodes = new Map([...result.transformNodes, ...result.meshes].map(n => [n.name, n]));
  const actions = new Map();
  for (const group of result.animationGroups) { group.stop(); actions.set(group.name.toLowerCase(), group); }
  if (spec.folder === 'sketchgirl') {
    const names = [...new Set(Object.values(spec.moves).flat())];
    await Promise.all(names.map(async name => {
      const container = await B.SceneLoader.LoadAssetContainerAsync(`models/${spec.folder}/`, name+'.glb', scene);
      const original = container.animationGroups[0];
      if (!original) throw new Error(`Animation missing: ${name}`);
      const group = new B.AnimationGroup(name, scene);
      for (const item of original.targetedAnimations) {
        const target = nodes.get(item.target.name);
        if (target && item.animation.targetProperty === 'rotationQuaternion') group.addTargetedAnimation(item.animation.clone(), target);
      }
      if (!group.targetedAnimations.length) throw new Error(`Animation is incompatible: ${name}`);
      actions.set(name, group);
      container.dispose();
    }));
  }
  const bone = key => nodes.get(spec[key]);
  for (const key of ['hips','spine','head','leftFoot','rightFoot','leftHand','rightHand']) if (!bone(key)) throw new Error(`Missing ${spec.name} bone: ${key}`);
  const f = { spec, name:spec.name, root, pose, model, bone, actions, hp:100, alive:true, state:'idle', busyUntil:0, cooldown:0.8 + index*0.2, target:null, pendingHit:null, reactionStart:0, current:null, previous:null, blend:1, history:new Map(), soleOffset: spec.folder === 'kungfu' ? .08 : .05 };
  f.play = (kind, loop=true) => {
    const name = pick(spec.moves[kind] || spec.moves.idle);
    const next = actions.get(name);
    if (!next) throw new Error(`Missing animation ${name}`);
    if (f.current === next && loop && next.isPlaying) return;
    if (f.previous && f.previous !== f.current) f.previous.stop();
    f.previous = f.current;
    next.stop(); next.start(loop, 1.2); next.setWeightForAllAnimatables(0);
    f.current = next; f.blend = 0;
  };
  f.advanceBlend = dt => {
    f.blend = Math.min(1, f.blend + dt / .14);
    f.current?.setWeightForAllAnimatables(f.blend);
    if (f.previous && f.previous !== f.current) { f.previous.setWeightForAllAnimatables(1-f.blend); if (f.blend === 1) { f.previous.stop(); f.previous = null; } }
  };
  f.face = () => { root.rotation.y = f.target.root.position.x > root.position.x ? Math.PI / 2 : -Math.PI / 2; };
  f.point = key => { const n = bone(key); n.computeWorldMatrix(true); return n.getAbsolutePosition().clone(); };
  f.center = () => {
    root.computeWorldMatrix(true); model.computeWorldMatrix(true);
    const hip = f.point('hips'); const delta = B.Vector3.TransformNormal(hip.subtract(root.getAbsolutePosition()), B.Matrix.Invert(root.getWorldMatrix()));
    pose.position.x -= delta.x; pose.position.z -= delta.z;
    root.computeWorldMatrix(true); model.computeWorldMatrix(true);
    const feet = Math.min(f.point('leftFoot').y, f.point('rightFoot').y);
    if (Number.isFinite(feet)) pose.position.y += f.soleOffset - feet;
  };
  f.play('idle');
  return f;
}
function resetRound() {
  fighters.forEach((f,i) => { f.hp=100; f.alive=true; f.state='idle'; f.busyUntil=0; f.cooldown=.7+i*.3; f.pendingHit=null; f.root.position.set(i === 0 ? -3 : 3,0,0); f.pose.rotation.x=0; f.target=fighters[1-i]; f.face(); f.play('idle'); });
}
function startAttack(f, now) {
  const kind = pick(['jab','heavy','kick']); const limb = f.spec.folder === 'sketchgirl' ? 'foot' : kind === 'kick' ? 'foot' : 'hand';
  f.play(kind,false); const group=f.current; const fps=group.targetedAnimations[0]?.animation.framePerSecond || 60; const duration=Math.max(.5,(group.to-group.from)/fps/1.2);
  f.state='attack'; f.busyUntil=now+duration*.95; f.cooldown=1+Math.random()*.5;
  f.pendingHit={target:f.target,limb,kind,at:now+duration*.3,until:now+duration*.75,history:new Map()};
  match.startAttack(f,f.target,{activeFrom:f.pendingHit.at,activeUntil:f.pendingHit.until,damage:kind==='jab'?15:25});
}
function updateFighter(f, dt, now) {
  f.advanceBlend(dt); f.center();
  if (!f.alive) return;
  f.cooldown -= dt;
  const p=f.pendingHit;
  if (p && p.target.alive) {
    const keys=p.limb==='foot'?['leftFoot','rightFoot']:['leftHand','rightHand'];
    const touch=findContact(keys.map(key=>({key,p:f.point(key)})),['hips','spine','head'].map(key=>p.target.point(key)),p.limb==='foot'?.38:.34,p.history);
    if (now>=p.at && now<=p.until) {
      const hit=match.tryHit(f,p.target,now,!!touch);
      if (hit) { f.pendingHit=null; if (!hit.dodged) showImpact(touch.point,now); if (!hit.dodged && !hit.blocked) { const t=p.target; t.pendingHit=null; t.state=t.hp<=0?'death':'hit'; t.alive=t.hp>0; t.reactionStart=now; t.busyUntil=now+.585; t.pose.rotation.x=t.alive?-.15:0; t.play('idle'); } }
    }
    if (now>p.until) f.pendingHit=null;
  }
  if (f.state==='hit' || f.state==='death') { const t=Math.min(1,(now-f.reactionStart)/.5); f.pose.rotation.x=-(f.state==='death'?1.25*t:.35*Math.sin(Math.PI*(.14+.86*t))); }
  else f.pose.rotation.x=0;
  if (now<f.busyUntil) return;
  if (f.state==='attack') { match.cancelAttack(f); f.pendingHit=null; f.state='idle'; f.play('idle'); }
  if (f.state==='hit'||f.state==='dodge') { f.state='idle'; f.play('idle'); }
  if (!f.target.alive) return;
  f.face(); const dx=f.target.root.position.x-f.root.position.x; const dist=Math.abs(dx);
  if (f.target.state==='attack' && dist<2.3 && f.state!=='guard' && Math.random()<.025) {
    if (Math.random()<.3) { f.state='dodge'; f.play('dodge',false); f.busyUntil=now+.6; return; }
    f.state='guard'; f.play('idle');
  }
  if (f.state==='guard' && f.target.state==='attack') return;
  if (f.state==='guard') { f.state='idle'; f.cooldown=.3; }
  if (dist>1.15) { if(f.state!=='walk'){f.state='walk';f.play('walk');} f.root.position.x += Math.sign(dx)*Math.min(1.5*dt,dist-1.15); }
  else if (f.cooldown<=0) startAttack(f,now);
  else if(f.state!=='idle'){f.state='idle';f.play('idle');}
}
async function main() {
  status.textContent='載入 Babylon.js 角色與動作中…';
  fighters.push(...await Promise.all(defs.map(loadFighter)));
  match=new FightingEngine(fighters,{startTime:performance.now()/1000,onRoundStart:resetRound,onRoundEnd:()=>fighters.forEach(f=>{f.pendingHit=null; if(f.alive){f.state='idle';f.play('idle');}})});
  resize();
  scene.onAfterAnimationsObservable.add(()=>{
    const now=performance.now()/1000,dt=Math.min(engine.getDeltaTime()/1000,.05);
    for (let i=sparks.length-1;i>=0;i--) if (now>=sparks[i].until) { sparks[i].mesh.dispose(); sparks.splice(i,1); }
    if (now < impactPauseUntil) return;
    match.step(now);
    if(match.phase==='fight') {
      fighters.forEach(f=>updateFighter(f,dt,now));
      const a=fighters[0],b=fighters[1],diff=b.root.position.x-a.root.position.x;
      if(Math.abs(diff)<.95){const push=(.95-Math.abs(diff))/2,side=diff>=0?1:-1;a.root.position.x-=push*side;b.root.position.x+=push*side;}
      fighters.forEach(f=>{f.root.position.x=B.Scalar.Clamp(f.root.position.x,-4.2,4.2);});
    } else fighters.forEach(f=>{f.advanceBlend(dt);f.center(); if(!f.alive)f.pose.rotation.x=-1.25*Math.min(1,(now-f.reactionStart)/.45);});
    fighters.forEach((f,i)=>document.getElementById(`health${i}`).style.width=`${Math.max(0,f.hp)}%`);
    document.getElementById('timer').textContent=Math.ceil(match.remaining); document.getElementById('score').textContent=match.scores.join(' : ');
    status.textContent=match.phase==='ready'?`第 ${match.round} 回合 — 準備！`:match.phase==='fight'?'':match.result;
  });
  engine.runRenderLoop(()=>{ scene.animationsEnabled = performance.now()/1000 >= impactPauseUntil; scene.render(); });
}
addEventListener('resize',()=>{engine.resize();resize();});
main().catch(error=>{console.error(error);status.textContent=`載入失敗：${error.message}`;});
