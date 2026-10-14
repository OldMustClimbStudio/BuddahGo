"""Generate the BuddahGo design-table workbooks from the effective (serialized) values in the project.

Usage: python build_tables.py <repo_root> <out_dir>
The build_* functions describe the v1 (per-component) layout; v2_layout.migrate_all reshapes the result into the
committed v2 layout before writing, so a rebuilt baseline matches Assets/Design/Tables sheet for sheet.
Naming rules (see Docs/design-tables/excel-spec.md):
  workbook = <Domain>.xlsx in Tables/<Domain>/ ; sheet = PascalCase record name ; field = camelCase (C# field) ;
  ids = snake_case ; text keys = domain.entity.field ; key columns first, doc columns last.
"""
import os, sys, re, csv, json
import tablemodel as TM
import v2_layout as V2
import extract as X
from extract import vec, color, curve, fmt, boolv, enum_array_hex, guid_path

ROOT, OUT = sys.argv[1], sys.argv[2]
X.set_root(ROOT)

BP = 'Assets/Character/Prefab/Buddah.prefab'
RM = 'Assets/Scenes/RaceMap.unity'
PS = 'Assets/Scenes/PropertySelection.unity'
MM = 'Assets/Scenes/MainMenu.unity'
UIP = 'Assets/UI/UIprefap.prefab'
RSM = 'Assets/UIMenus/Lobby/RoomStateManager.prefab'
MAIN = 'Assets/Data/Config/ProjectConfigDatabase_Main.asset'
SKILLS = 'Assets/Skills_Assets/Skills/'

MODEL = TM.Model()
text_rows = {}   # key -> (zh, en, comment, context)

def T(key, zh, en='', comment='', context=''):
    """Register a localized text and return its key."""
    if key in text_rows and text_rows[key][0] != zh:
        raise ValueError(f'text key collision {key}: {text_rows[key][0]!r} vs {zh!r}')
    text_rows[key] = (zh, en, comment, context)
    return key

def sheet(wb_name, name, cols, rows, record='(新增)', source='', notes=''):
    """cols: list of (var, type, group, comment). rows: list of dicts keyed by var. Collected into MODEL; written at the end."""
    return MODEL.add(TM.Sheet(wb_name, name, cols, rows, record, source, notes))

def rowsof(fields, cols, extra=None):
    """Build one row dict from a serialized-field dict, converting by declared type."""
    r = {}
    for var, typ, grp, com in cols:
        if var not in fields:
            continue
        v = fields[var]
        if typ.startswith('bool'):
            r[var] = boolv(v)
        elif typ.startswith('vector3'):
            r[var] = vec(v)
        elif typ.startswith('vector2'):
            r[var] = vec(v, 2)
        elif typ.startswith('color'):
            r[var] = color(v)
        elif typ.startswith('curve'):
            r[var] = curve(v)
        elif typ.startswith('list'):
            r[var] = '|'.join(fmt(x) for x in (v or []))
        else:
            r[var] = v
    if extra:
        r.update(extra)
    return r

def C(var, typ, com, grp=''):
    return (var, typ, grp, com)

SKILL_ORDER = ['acceleration', 'slowtrap', 'blackcurtain', 'giant', 'push_projectile_hands', 'reverseturn',
               'acceleration_anti', 'slowtrap_anti', 'anti_blackcurtain', 'giant_anti', 'push_projectile_hands_anti', 'reverseturn_anti']
SO_FILE = {'acceleration': 'Skill_Acceleration', 'slowtrap': 'Skill_SlowTrap', 'blackcurtain': 'Skill_BlackCurtain', 'giant': 'Skill_Giant',
           'push_projectile_hands': 'Skill_PushProjectileHands', 'reverseturn': 'Skill_ReverseTurn',
           'acceleration_anti': 'Skill_Acceleration_Anti', 'slowtrap_anti': 'Skill_SlowTrap_Anti', 'anti_blackcurtain': 'Skill_BlackCurtain_Anti',
           'giant_anti': 'Skill_Giant_Anti', 'push_projectile_hands_anti': 'Skill_PushProjectileHands_Anti', 'reverseturn_anti': 'Skill_ReverseTurn_Anti'}
SO = {sid: X.mono_asset(SKILLS + f + '.asset') for sid, f in SO_FILE.items()}
main = X.mono_asset(MAIN)

# =====================================================================================
# Skill.xlsx
# =====================================================================================
def build_skill():
    WB = 'Skill'
    # --- SkillDefinition (MAIN) ---
    cols = [C('skillId', 'string#key', '技能唯一 id，snake_case'), C('displayNameKey', 'string#ref=UI/Text.key', '显示名文案 key'),
            C('descriptionKey', 'string#ref=UI/Text.key', '描述文案 key'), C('behaviorType', 'enum:SkillBehaviorType', '行为类型（目前无运行时读取，由 SO 子类决定）'),
            C('iconKey', 'string', '图标 key（目前 UI 未解析，见 UI/SkillIcon）'), C('isSelectable', 'bool', '是否可选（无读取方，选池看 SkillSelectionRule.poolType）'),
            C('sortOrder', 'int', '选择界面排序'), C('tags', 'list,string#sep=|', '标签（无读取方）'), C('unlockState', 'enum:SkillUnlockState', 'unlocked / locked / hidden')]
    # MAIN is an exporter product in the v2 shape (skills + localizedTexts); rebuild the v1 rows from it so the
    # v2 layout pass below merges them back the same way the committed workbooks were produced.
    texts = {t['key']: t.get('zh', '') for t in main.get('localizedTexts', [])}
    rows = []
    for rec in main['skills']:
        sid = rec['skillId']
        nk = T(f'skill.{sid}.name', texts.get(rec['displayNameKey'], ''), '', '技能显示名', 'SkillDefinition.displayName')
        dk = T(f'skill.{sid}.desc', texts.get(rec['descriptionKey'], ''), '', '技能描述', 'SkillDefinition.description')
        rows.append({'skillId': sid, 'displayNameKey': nk, 'descriptionKey': dk, 'behaviorType': '', 'iconKey': '',
                     'isSelectable': not sid.endswith('_anti'), 'sortOrder': rec['sortOrder'], 'tags': '', 'unlockState': rec['unlockState']})
    sheet(WB, 'SkillDefinition', cols, rows, 'SkillDefinitionRecord（displayName/description 改为 key）', MAIN)

    # --- SkillBalance (MAIN + code constants) ---
    cols = [C('skillId', 'string#key#ref=SkillDefinition.skillId', ''), C('cooldownSeconds', 'float#range=0,120', '冷却，从请求时刻+前摇起算'),
            C('castLockSeconds', 'float#range=0,120', '施法锁定'), C('obsessionGain', 'float#range=0,1000', '施法增加的执念（反噬行无效：按请求技能取值）'),
            C('antiSkillId', 'string#ref=SkillDefinition.skillId', '反噬变体 id，普通技能必填'), C('castConfirmDelaySeconds', 'float#range=0,5', '施法前摇（原 SkillExecutor 常量，普通 1 / 反噬 1）')]
    rows = []
    for rec in main['skills']:
        rows.append({'skillId': rec['skillId'], 'cooldownSeconds': rec['cooldownSeconds'], 'castLockSeconds': rec['castLockSeconds'],
                     'obsessionGain': rec['obsessionGain'], 'antiSkillId': rec.get('antiSkillId') or '', 'castConfirmDelaySeconds': rec.get('castConfirmDelaySeconds', 1)})
    sheet(WB, 'SkillBalance', cols, rows, 'SkillBalanceRecord + castConfirmDelaySeconds', f'{MAIN}; SkillExecutor.cs:51-52',
          '如来神掌 SO 为 18/15，运行时生效的是配置 12/10')

    # --- SkillEffectParam (from SOs) ---
    cols = [C('skillId', 'string#key#ref=SkillDefinition.skillId', ''), C('effectType', 'string#key', '效果类型，与技能家族同名'),
            C('paramKey', 'string#key', '参数名，camelCase 与 SO 字段一致'), C('paramValue', 'string', '值；vector3 写 x,y,z'),
            C('valueType', 'string', '校验用类型', 'doc'), C('unit', 'string', '单位', 'doc')]
    fam = {
        'acceleration': ('Acceleration', [('extraForwardForce', 'float', 'N'), ('extraMaxSpeed', 'float', 'm/s'), ('durationSeconds', 'float', 's')]),
        'acceleration_anti': ('AccelerationAnti', [('slowForwardForce', 'float', 'N'), ('slowMaxSpeed', 'float', 'm/s'), ('durationSeconds', 'float', 's')]),
        'slowtrap': ('SlowTrap', [('zoneDurationSeconds', 'float', 's'), ('perTargetReapplyCooldown', 'float', 's'), ('affectCaster', 'bool', ''),
                                  ('slowForwardForce', 'float', 'N'), ('slowMaxSpeed', 'float', 'm/s'), ('slowDurationSeconds', 'float', 's')]),
        'slowtrap_anti': ('SlowTrapAnti', [('rootDurationSeconds', 'float', 's'), ('extraForwardForce', 'float', 'N'), ('extraMaxSpeed', 'float', 'm/s'), ('accelerationDurationSeconds', 'float', 's')]),
        'blackcurtain': ('BlackCurtain', [('expandDurationSeconds', 'float', 's'), ('holdDurationSeconds', 'float', 's'), ('fadeOutDurationSeconds', 'float', 's'), ('maxOpacity', 'float', '0-1')]),
        'anti_blackcurtain': ('BlackCurtainAnti', [('expandDurationSeconds', 'float', 's'), ('holdDurationSeconds', 'float', 's'), ('fadeOutDurationSeconds', 'float', 's'), ('maxOpacity', 'float', '0-1')]),
        'giant': ('Giant', [('scaleMultiplier', 'float', 'x'), ('durationSeconds', 'float', 's'), ('growDurationSeconds', 'float', 's'), ('shrinkDurationSeconds', 'float', 's'),
                            ('massMultiplier', 'float', 'x'), ('forwardForceMultiplier', 'float', 'x')]),
        'giant_anti': ('GiantAnti', [('scaleMultiplier', 'float', 'x'), ('durationSeconds', 'float', 's'), ('shrinkDurationSeconds', 'float', 's'), ('restoreDurationSeconds', 'float', 's')]),
        'push_projectile_hands': ('ProjectileHands', [('buffDurationSeconds', 'float', 's'), ('buildUpSeconds', 'float', 's'), ('chargedPushCooldownSeconds', 'float', 's'),
                                                      ('delayedPushActionSeconds', 'float', 's'), ('projectileForwardOffset', 'float', 'm'), ('projectileHeightOffset', 'float', 'm'),
                                                      ('projectileSpeed', 'float', 'm/s'), ('projectileLifetimeSeconds', 'float', 's'), ('projectileImpulseStrength', 'float', 'N·s'),
                                                      ('projectileColliderSize', 'vector3', 'm'), ('ignoreSolidWorld', 'bool', '(无效字段)')]),
        'push_projectile_hands_anti': ('ProjectileHandsAnti', [('buildUpSeconds', 'float', 's'), ('projectileCount', 'int', ''), ('projectileSpacing', 'float', 'm'),
                                                               ('additionalForwardSpawnOffset', 'float', 'm'), ('additionalHeightSpawnOffset', 'float', 'm'), ('projectileSpeed', 'float', 'm/s'),
                                                               ('projectileLifetimeSeconds', 'float', 's'), ('projectileImpulseStrength', 'float', 'N·s'), ('projectileColliderSize', 'vector3', 'm'),
                                                               ('hitTurnTorqueImpulse', 'float', '')]),
        'reverseturn': ('ReverseTurn', [('invertDurationSeconds', 'float', 's')]),
        'reverseturn_anti': ('ReverseTurnAnti', [('invertDurationSeconds', 'float', 's')]),
    }
    rows = []
    for sid in SKILL_ORDER:
        et, params = fam[sid]
        so = SO[sid]
        for key, vt, unit in params:
            if key not in so:
                continue
            v = so[key]
            pv = vec(v) if vt == 'vector3' else ('true' if boolv(v) else 'false') if vt == 'bool' else fmt(v)
            rows.append({'skillId': sid, 'effectType': et, 'paramKey': key, 'paramValue': pv, 'valueType': vt, 'unit': unit})
    # code-side effect constants
    rows += [
        {'skillId': 'slowtrap', 'effectType': 'SlowTrap', 'paramKey': 'zoneRadius', 'paramValue': '7.5', 'valueType': 'float', 'unit': 'm（SlowTrapZone.prefab scale 15 × 胶囊半径 0.5）'},
        {'skillId': 'push_projectile_hands', 'effectType': 'ProjectileHands', 'paramKey': 'flightEndSpeedFraction', 'paramValue': '0.2', 'valueType': 'float', 'unit': 'ChargedHandProjectileRuntime.cs:38'},
        {'skillId': 'push_projectile_hands', 'effectType': 'ProjectileHands', 'paramKey': 'progressCompletionFraction', 'paramValue': '0.8', 'valueType': 'float', 'unit': 'ChargedHandProjectileRuntime.cs:37'},
        {'skillId': 'push_projectile_hands_anti', 'effectType': 'ProjectileHandsAnti', 'paramKey': 'reverseDirection', 'paramValue': 'true', 'valueType': 'bool', 'unit': 'BuddahHandControl.cs:967'},
        {'skillId': 'push_projectile_hands_anti', 'effectType': 'ProjectileHandsAnti', 'paramKey': 'allowSelfHit', 'paramValue': 'true', 'valueType': 'bool', 'unit': 'BuddahHandControl.cs:968'},
        {'skillId': 'push_projectile_hands_anti', 'effectType': 'ProjectileHandsAnti', 'paramKey': 'ignoreSolidWorld', 'paramValue': 'true', 'valueType': 'bool', 'unit': 'BuddahHandControl.cs:969'},
        {'skillId': 'reverseturn', 'effectType': 'ReverseTurn', 'paramKey': 'targetScope', 'paramValue': 'AllOthers', 'valueType': 'enum:SkillTargetScope', 'unit': 'Skill_ReverseTurn.cs:26-39'},
        {'skillId': 'reverseturn_anti', 'effectType': 'ReverseTurnAnti', 'paramKey': 'targetScope', 'paramValue': 'Self', 'valueType': 'enum:SkillTargetScope', 'unit': ''},
        {'skillId': 'blackcurtain', 'effectType': 'BlackCurtain', 'paramKey': 'casterSeesTrackEdge', 'paramValue': 'true', 'valueType': 'bool', 'unit': 'BlackCurtainPresentation.cs:35 (caster XOR anti)'},
        {'skillId': 'anti_blackcurtain', 'effectType': 'BlackCurtainAnti', 'paramKey': 'casterSeesTrackEdge', 'paramValue': 'false', 'valueType': 'bool', 'unit': ''},
        {'skillId': 'giant', 'effectType': 'Giant', 'paramKey': 'scalesMaxSpeed', 'paramValue': 'false', 'valueType': 'bool', 'unit': 'Resolver.cs:30-38'},
        {'skillId': 'giant_anti', 'effectType': 'GiantAnti', 'paramKey': 'massMultiplier', 'paramValue': '1', 'valueType': 'float', 'unit': 'Skill_Giant_Anti.cs:18'},
        {'skillId': 'giant_anti', 'effectType': 'GiantAnti', 'paramKey': 'forwardForceMultiplier', 'paramValue': '1', 'valueType': 'float', 'unit': 'Skill_Giant_Anti.cs:18'},
        {'skillId': 'acceleration', 'effectType': 'Acceleration', 'paramKey': 'stackRule', 'paramValue': 'MaxMagnitudeMaxDuration', 'valueType': 'enum:StackPolicy', 'unit': 'MovementAccelerationEffect.cs:38-46'},
    ]
    sheet(WB, 'SkillEffectParam', cols, rows, 'SkillEffectParamRecord（接通运行时读取）', f'{SKILLS}*.asset + 代码常量', 'paramValue 以 SO 实际值为准，代码默认值已忽略')

    # --- SkillPresentation ---
    cols = [C('skillId', 'string#key#ref=SkillDefinition.skillId', ''), C('vfxId', 'string#ref=VfxRegistry.vfxId', '空=不播'), C('vfxLocalOffset', 'vector3', ''),
            C('vfxLocalEuler', 'vector3', ''), C('vfxDurationSeconds', 'float', '0=跟随效果时长'), C('vfxStopBeforeEndSeconds', 'float', '提前停止发射'),
            C('observersFeelEventId', 'string#ref=FeelEvent.eventId', ''), C('observersFeelStopEventId', 'string#ref=FeelEvent.eventId', ''),
            C('fovBoost', 'float', '施法者 FOV 增量'), C('fovRampInSeconds', 'float', ''), C('fovSettleSeconds', 'float', ''),
            C('fovRampInCurve', 'curve', 'time,value,inSlope,outSlope|…'), C('fovSettleCurve', 'curve', ''),
            C('effective', 'bool', '该行 vfx 字段是否真的被播放', 'doc')]
    rows = []
    dead_vfx = {'acceleration_anti', 'reverseturn', 'reverseturn_anti'}
    for sid in SKILL_ORDER:
        so = SO[sid]
        rows.append({'skillId': sid, 'vfxId': so.get('vfxId', ''), 'vfxLocalOffset': vec(so.get('vfxLocalOffset')), 'vfxLocalEuler': vec(so.get('vfxLocalEuler')),
                     'vfxDurationSeconds': so.get('vfxDurationSeconds', ''), 'vfxStopBeforeEndSeconds': so.get('vfxStopPlayingBeforeEndSeconds', ''),
                     'observersFeelEventId': so.get('observersFeelEventId', ''), 'observersFeelStopEventId': so.get('observersFeelStopEventId', ''),
                     'fovBoost': so.get('localCasterBuffFovBoost', ''), 'fovRampInSeconds': so.get('localCasterBuffFovRampInSeconds', ''), 'fovSettleSeconds': so.get('localCasterBuffFovSettleSeconds', ''),
                     'fovRampInCurve': curve(so.get('localCasterBuffFovRampInCurve')), 'fovSettleCurve': curve(so.get('localCasterBuffFovSettleCurve')),
                     'effective': sid not in dead_vfx})
    sheet(WB, 'SkillPresentation', cols, rows, '(新增) SkillPresentationRecord', f'{SKILLS}*.asset', 'ReverseTurn/AccelerationAnti 的 vfx 字段代码从不播放')

    # --- VfxRegistry ---
    vdb = X.mono_asset('Assets/Skills_Assets/SkillVfxDatabase.asset')
    warm = X.one(RM, 'VfxStartupWarmup')
    warm_paths = {guid_path(p) for p in warm.get('vfxPrefabs', [])}
    cols = [C('vfxId', 'string#key', ''), C('prefabPath', 'string#path', '相对工程路径'), C('followTargetPosition', 'bool', 'SkillVfxAttachmentSettings'),
            C('inheritTargetRotation', 'bool', ''), C('inheritTargetScale', 'bool', ''), C('warmup', 'bool', '启动预热（VfxStartupWarmup）'), C('referencedBy', 'string', '', 'doc')]
    rows = []
    for e in vdb['entries']:
        p = guid_path(e['prefab'])
        att = {'followTargetPosition': True, 'inheritTargetRotation': True, 'inheritTargetScale': False}
        try:
            a = X.one(p, 'SkillVfxAttachmentSettings')
            att = {k: boolv(a.get(k, att[k])) for k in att}
        except Exception:
            pass
        rows.append({'vfxId': e['vfxId'], 'prefabPath': p, **att, 'warmup': p in warm_paths, 'referencedBy': ','.join(s for s in SKILL_ORDER if SO[s].get('vfxId') == e['vfxId'])})
    for missing in ['accel_anti_vfx', 'reverse_turn_trap_vfx', 'reverse_turn_trap_anti_vfx']:
        rows.append({'vfxId': missing, 'prefabPath': '', 'followTargetPosition': True, 'inheritTargetRotation': True, 'inheritTargetScale': False, 'warmup': False,
                     'referencedBy': '(SO 引用但库中无条目，代码也不播放)'})
    sheet(WB, 'VfxRegistry', cols, rows, '(新增) VfxRegistryRecord ← SkillVfxDatabase', 'Assets/Skills_Assets/SkillVfxDatabase.asset; RaceMap VfxStartupWarmup')

    # --- FeelEvent ---
    router = X.one(BP, 'SkillFeelRouter')
    names = X.go_names(BP)
    cols = [C('eventId', 'string#key', ''), C('playerObjectName', 'string', 'Buddah.prefab 内 MMF_Player 所在物体名；空=未接线'), C('usedBy', 'string', '', 'doc')]
    rows = []
    for e in router.get('entries', []):
        fid = (e.get('feedbackPlayer') or {}).get('fileID', 0)
        pname = X.resolve_ref_name(BP, fid) if fid else ''
        rows.append({'eventId': e['eventId'], 'playerObjectName': pname, 'usedBy': ''})
    used = {'Skill_Reflection_Shared': 'SkillExecutor.cs:53', 'Skill_Reflection_Local': 'SkillExecutor.cs:54', 'Anti_Reflection_Shared': 'SkillExecutor.cs:55', 'Anti_Reflection_Local': 'SkillExecutor.cs:56',
            'acceleration_shared': 'Skill_Acceleration 默认（SO 中为空）'}
    for r in rows:
        r['usedBy'] = used.get(r['eventId'], '')
    for eid, by in [('acceleration_local', 'OwnerMovementEffectApplier.cs:25,52'), ('acceleration_anti_observers', 'Skill_Acceleration_Anti 默认'),
                    ('blackcurtain_observers', 'Skill_BlackCurtain.asset'), ('blackcurtain_anti_observers', 'Skill_BlackCurtain_Anti.asset')]:
        rows.append({'eventId': eid, 'playerObjectName': '', 'usedBy': by + '（router 未注册）'})
    sheet(WB, 'FeelEvent', cols, rows, '(新增) FeelEventRecord ← SkillFeelRouter.entries', BP)

    # --- SkillSelectionRule / DefaultLoadout (MAIN) ---
    cols = [C('ruleSetId', 'string#key', '空=default'), C('skillId', 'string#key#ref=SkillDefinition.skillId', ''), C('modeTag', 'string#key', '空=全部'), C('mapTag', 'string#key', '空=全部'),
            C('poolType', 'enum:SkillSelectionPoolType#key', 'Selectable/Fallback/Default/Hidden/TestOnly；同一技能可同时在多个池'), C('isEnabled', 'bool', ''), C('allowDuplicate', 'bool', ''), C('weight', 'float', '（无读取方）', 'doc')]
    pool = {0: 'Selectable', 1: 'Fallback', 2: 'Default', 3: 'Hidden', 4: 'TestOnly'}
    rows = [{'ruleSetId': r['ruleSetId'], 'skillId': r['skillId'], 'modeTag': r.get('modeTag') or '', 'mapTag': r.get('mapTag') or '', 'poolType': pool[r['poolType']],
             'isEnabled': boolv(r['isEnabled']), 'allowDuplicate': boolv(r['allowDuplicate']), 'weight': r['weight']} for r in main['skillSelectionRules']]
    sheet(WB, 'SkillSelectionRule', cols, rows, 'SkillSelectionRuleRecord', MAIN)
    cols = [C('loadoutId', 'string#key', ''), C('ruleSetId', 'string', ''), C('modeTag', 'string', ''), C('slot0SkillId', 'string#ref=SkillDefinition.skillId', ''),
            C('slot1SkillId', 'string#ref=SkillDefinition.skillId', ''), C('slot2SkillId', 'string#ref=SkillDefinition.skillId', '')]
    rows = [{k: r.get(k, '') for k in ['loadoutId', 'ruleSetId', 'modeTag', 'slot0SkillId', 'slot1SkillId', 'slot2SkillId']} for r in main['defaultLoadouts']]
    sheet(WB, 'DefaultLoadout', cols, rows, 'DefaultLoadoutRecord', MAIN, '运行时优先取此表（SkillLoadout.cs:90-101）；prefab 的 _defaultSkillIds 第 3 槽 blackcurtain 只是兜底')

    # --- ComboBinding ---
    ci = X.one(BP, 'ComboSkillInput')
    tok = {0: 'W', 1: 'Up'}
    cols = [C('slotIndex', 'int#key', '技能槽 0-2'), C('name', 'string', ''), C('sequence', 'list,enum:ComboToken#sep=,', '按键序列 W / Up')]
    rows = [{'slotIndex': b['slotIndex'], 'name': b['name'], 'sequence': ','.join(tok[t] for t in enum_array_hex(b['sequence']))} for b in ci['bindings']]
    sheet(WB, 'ComboBinding', cols, rows, '(新增) ComboBindingRecord ← ComboSkillInput.bindings', BP, 'HUD SkillSlot prefab 的 tokens 由此表生成')

    # --- HandPush ---
    hc = X.one(BP, 'BuddahHandControl')
    cube = X.first_doc('Assets/Character/Prefab/Cube.prefab', 'BoxCollider')
    cube_tf = [d for d in X.docs('Assets/Character/Prefab/Cube.prefab') if d['kind'] == 'Transform'][0]['data']
    cols = [C('profileId', 'string#key', ''), C('rotationSpeedDegPerSec', 'float', '手部旋转速度（AI 也读）'), C('pushWindupSeconds', 'float', '前摇'), C('pushLifetimeSeconds', 'float', 'hitbox 存活'),
            C('pushForwardOffset', 'float', ''), C('pushSideOffset', 'float', ''), C('pushLeftSideBias', 'float', ''), C('pushRightSideBias', 'float', ''), C('pushHeightOffset', 'float', ''),
            C('pushImpulseStrength', 'float', '冲量'), C('pushCooldownSeconds', 'float', '普通推掌冷却'), C('hitboxSize', 'vector3', 'Cube.prefab BoxCollider size × scale'),
            C('hitboxCenter', 'vector3', ''), C('leftHandTriggerName', 'string', 'Animator trigger', 'doc'), C('rightHandTriggerName', 'string', '', 'doc')]
    sz = cube['m_Size']; sc = cube_tf['m_LocalScale']
    rows = [rowsof(hc, cols, {'profileId': 'default', 'hitboxSize': vec({'x': sz['x']*sc['x'], 'y': sz['y']*sc['y'], 'z': sz['z']*sc['z']}), 'hitboxCenter': vec(cube['m_Center'])})]
    sheet(WB, 'HandPush', cols, rows, '(新增) HandPushRecord ← BuddahHandControl', f'{BP}; Assets/Character/Prefab/Cube.prefab')

    # --- Obsession ---
    ob = X.one(BP, 'ObsessionFigure')
    cols = [C('profileId', 'string#key', ''), C('maxValue', 'float', '上限'), C('minValue', 'float', ''), C('initialValue', 'float', '出生初值'), C('drainPerSecond', 'float', '每秒自然下降'),
            C('catchupRecoveryA', 'float', '追赶加速衰减系数 R=a·gap²'), C('backfirePMaxPercent', 'float', '反噬概率上限 %'), C('backfireK', 'float', 'sigmoid 陡度'),
            C('backfireMidpointX', 'float', 'sigmoid 中点'), C('backfireKeepPMaxAtOrAboveX', 'float', '达到即取上限')]
    sheet(WB, 'Obsession', cols, [rowsof(ob, cols, {'profileId': 'default'})], '(新增) ObsessionRecord ← ObsessionFigure', BP, '替代未读取的 GlobalRule 键 catchup_recovery_a / obsession_recovery_baseline')

    # --- BlackCurtainPresentation (screen effect) ---
    bv = X.one(RM, 'BlackCurtainViewController')
    so = SO['blackcurtain']
    cols = [C('profileId', 'string#key', ''), C('trackEdgeFadeInDelaySeconds', 'float', ''), C('hideOtherPlayersForVictim', 'bool', ''), C('disableOutdoorVolumeDuringBlackCurtain', 'bool', ''),
            C('expansionDelaySeconds', 'float', 'BlackCurtainScreenEffect.cs:8'), C('maxBlackHoleStrength', 'float', ':7'), C('maxExpansionProgress', 'float', ':9'),
            C('centerWorldOffset', 'vector3', 'SO'), C('materialPath', 'string#path', '', 'doc'), C('outdoorVolumeObjectName', 'string', '场景物体名', 'doc'), C('blackCurtainActiveVolumeObjectName', 'string', '', 'doc')]
    rows = [{'profileId': 'default', 'trackEdgeFadeInDelaySeconds': bv.get('trackEdgeFadeInDelay'), 'hideOtherPlayersForVictim': boolv(bv.get('hideOtherPlayersForVictim')),
             'disableOutdoorVolumeDuringBlackCurtain': boolv(bv.get('disableOutdoorVolumeDuringBlackCurtain')), 'expansionDelaySeconds': 0.5, 'maxBlackHoleStrength': 15, 'maxExpansionProgress': 2,
             'centerWorldOffset': vec(so.get('centerWorldOffset')), 'materialPath': guid_path(so.get('screenMaterial') or so.get('material') or {}),
             'outdoorVolumeObjectName': bv.get('outdoorVolumeObjectName'), 'blackCurtainActiveVolumeObjectName': bv.get('blackCurtainActiveVolumeObjectName')}]
    sheet(WB, 'BlackCurtainPresentation', cols, rows, '(新增) BlackCurtainPresentationRecord', f'{RM} BlackCurtainViewController; BlackCurtainScreenEffect.cs')

# =====================================================================================
# Movement.xlsx
# =====================================================================================
def build_movement():
    WB = 'Movement'
    cfg = X.one(BP, 'BuddahPredictedMotorConfig')
    rb = X.first_doc(BP, 'Rigidbody')
    caps = [d['data'] for d in X.docs(BP) if d['kind'] == 'CapsuleCollider' and not d['data'].get('m_IsTrigger')][0]
    dyn = X.first_doc('ProjectSettings/DynamicsManager.asset', 'PhysicsManager')
    cols = [C('profileId', 'string#key', '目前只有 default（玩家与 AI 共用）'), C('forwardForce', 'float', 'ForceMode.Force'), C('maxSpeed', 'float', '水平限速 m/s'), C('turnTorque', 'float', ''),
            C('turnDecayPerSecond', 'float', '无输入时角速度回正 rad/s²'), C('turnInputMultiplier', 'float', 'V2 预测马达值（legacy 为 1）'), C('autoThrottle', 'float', '油门恒 1'),
            C('mass', 'float', 'Rigidbody'), C('linearDrag', 'float', ''), C('angularDrag', 'float', ''), C('maxAngularVelocity', 'float', '项目默认 DynamicsManager'),
            C('colliderRadius', 'float', ''), C('colliderHeight', 'float', ''), C('colliderCenter', 'vector3', ''), C('physicMaterial', 'string#ref=Global/PhysicsMaterial.materialId', ''),
            C('visualRootScale', 'float', 'VisualRoot localScale', 'doc')]
    vroot = [d['data'] for d in X.docs(BP) if d['kind'] == 'Transform' and X.go_names(BP).get((d['data'].get('m_GameObject') or {}).get('fileID')) == 'VisualRoot']
    rows = [rowsof(cfg, cols, {'profileId': 'default', 'autoThrottle': 1, 'mass': rb['m_Mass'], 'linearDrag': rb['m_Drag'], 'angularDrag': rb['m_AngularDrag'],
                               'maxAngularVelocity': dyn['m_DefaultMaxAngularSpeed'], 'colliderRadius': caps['m_Radius'], 'colliderHeight': caps['m_Height'], 'colliderCenter': vec(caps['m_Center']),
                               'physicMaterial': 'Character', 'visualRootScale': vroot[0]['m_LocalScale']['x'] if vroot else ''})]
    sheet(WB, 'MovementBase', cols, rows, '(新增) MovementBaseRecord ← BuddahPredictedMotorConfig + Rigidbody', BP, 'legacy BuddahMovement 的副本不再使用')

    cols = [C('impulseSource', 'enum:ImpulseSourceType#key', 'MeleePush / Projectile / ChargedProjectile'), C('graceSeconds', 'float', '受击宽限'), C('extraMaxSpeed', 'float', '宽限期临时限速增量'), C('impulseMultiplier', 'float', '按来源倍率（当前代码无区分=1）')]
    rows = [{'impulseSource': s, 'graceSeconds': cfg['pushGraceSeconds'], 'extraMaxSpeed': cfg['pushExtraMaxSpeed'], 'impulseMultiplier': 1} for s in ['MeleePush', 'Projectile', 'ChargedProjectile']]
    sheet(WB, 'PushReaction', cols, rows, '(新增) PushReactionRecord', f'{BP} BuddahPredictedMotorConfig; BuddahPredictedImpulseSourceType.cs')

    cols = [C('modifierType', 'enum:ModifierCommandType#key', ''), C('stackPolicy', 'enum:StackPolicy', '当前代码：持续取较大值，幅度后写覆盖'), C('minScaleMultiplier', 'float', '倍率下限（代码 4 处重复 0.1）'),
            C('scalesMaxSpeed', 'bool', 'Giant 是否缩放限速'), C('resetOnSkillReset', 'bool', 'ResetActiveSkillModifiers 是否重置'), C('comment', 'string', '', 'doc')]
    mods = [('Acceleration', 'RefreshMaxDurationOverwriteMagnitude', '', '', True, 'ValueA=extraForwardForce ValueB=extraMaxSpeed ValueC=duration'),
            ('RootThenAcceleration', 'RefreshMaxDurationOverwriteMagnitude', '', '', True, '定身期 force/maxSpeed=0，速度清零'),
            ('InvertTurn', 'RefreshMaxDurationOverwriteMagnitude', '', '', True, 'FinalSteeringSign=-1 固定'),
            ('Scale', 'RefreshMaxDurationOverwriteMagnitude', 0.1, False, True, 'turnTorque×scale，force×forceMul，mass×massMul'),
            ('PushGrace', 'RefreshMaxDuration', '', '', False, ''), ('SteeringSuppression', 'RefreshMaxDuration', '', '', False, '转向清零'), ('RoomBypass', 'RefreshMaxDuration', '', '', False, '绕过房间闸门')]
    rows = [{'modifierType': a, 'stackPolicy': b, 'minScaleMultiplier': c, 'scalesMaxSpeed': d, 'resetOnSkillReset': e, 'comment': f} for a, b, c, d, e, f in mods]
    sheet(WB, 'ModifierRule', cols, rows, '(新增) ModifierRuleRecord', 'New_Buddah BuddahPredictedMotor.Events.cs:365-402; Resolver.cs')

    bm = X.one(BP, 'BuddahMovement')
    ric = X.one(BP, 'RaceBodyIntroStateController')
    cols = [C('profileId', 'string#key', ''), C('inheritSeconds', 'float', 'launchInheritTime'), C('blendSeconds', 'float', 'launchBlendTime，线性混合'), C('suppressTurnSeconds', 'float', 'RaceBodyIntroStateController.cs:560'),
            C('handoffLeadSeconds', 'float', 'defaultHandoffLeadTime'), C('minPreHandoffBypassSeconds', 'float', 'Events.cs:173 下限'), C('clearAngularVelocity', 'bool', ''),
            C('groundSnapMaxDistance', 'float', 'Config 默认（prefab 未序列化）'), C('groundProbeHeight', 'float', ''), C('velocitySampleDeltaSeconds', 'float', '', 'adv')]
    rows = [{'profileId': 'default', 'inheritSeconds': bm['launchInheritTime'], 'blendSeconds': bm['launchBlendTime'], 'suppressTurnSeconds': 0.15, 'handoffLeadSeconds': ric['defaultHandoffLeadTime'],
             'minPreHandoffBypassSeconds': 0.5, 'clearAngularVelocity': False, 'groundSnapMaxDistance': cfg.get('handoffGroundSnapMaxDistance', 1.5), 'groundProbeHeight': cfg.get('handoffGroundProbeHeight', 3),
             'velocitySampleDeltaSeconds': ric['velocitySampleDeltaSeconds']}]
    sheet(WB, 'LaunchHandoff', cols, rows, '(新增) LaunchHandoffRecord', f'{BP} BuddahMovement/RaceBodyIntroStateController; Events.cs')

    rs = X.one(BP, 'BuddahRespawn')
    cols = [C('teleportSource', 'enum:TeleportSourceType#key', 'DropRespawn / WrongWayCorrection / Manual'), C('contactSeconds', 'float', '持续接触多久触发'), C('cooldownSeconds', 'float', ''),
            C('heightOffset', 'float', ''), C('clearanceRadius', 'float', ''), C('liftStep', 'float', ''), C('maxLift', 'float', ''), C('floorTag', 'string#ref=Global/Tag.name', ''),
            C('snapProgress', 'bool', ''), C('zeroVelocity', 'bool', ''), C('resetModifiers', 'bool', ''), C('resetImpulseQueue', 'bool', ''), C('resetPushGrace', 'bool', ''), C('rebaseTrails', 'bool', '')]
    base = {'contactSeconds': rs['continuousContactRespawnSeconds'], 'cooldownSeconds': rs['postRespawnCooldownSeconds'], 'heightOffset': rs['respawnHeightOffset'], 'clearanceRadius': rs['respawnClearanceCheckRadius'],
            'liftStep': rs['additionalRespawnLiftStep'], 'maxLift': rs['maxAdditionalRespawnLift'], 'floorTag': rs['respawnFloorTag'], 'snapProgress': True, 'zeroVelocity': True, 'resetModifiers': True,
            'resetImpulseQueue': True, 'resetPushGrace': True, 'rebaseTrails': True}
    rows = [{'teleportSource': s, **base} for s in ['DropRespawn', 'WrongWayCorrection', 'Manual']]
    sheet(WB, 'RespawnRule', cols, rows, '(新增) RespawnRuleRecord ← BuddahRespawn + RespawnBridge 标志', f'{BP}; BuddahPredictionRespawnBridge.cs:295-301')

    lp = X.one(BP, 'LapProgress')
    rfm = X.one(RM, 'RaceFinishManager')
    cols = [C('trackId', 'string#key#ref=Match/Track.trackId', ''), C('lapsToFinish', 'int', 'RaceFinishManager 场景值'), C('crossCooldownSeconds', 'float', 'minimumCrossingCooldownSeconds'),
            C('maxProgressJumpWithoutCheckpoint01', 'float', ''), C('minForwardDot', 'float', ''), C('armDistanceMeters', 'float', 'minimumWorldDistanceFromStartToArmLap'),
            C('useSequentialCheckpoints', 'bool', ''), C('autoAdvanceSplineCheckpoints', 'bool', ''), C('requiredCheckpointCount', 'int', '须等于 TrackCheckpoint 行数'),
            C('fallbackCheckpointProgresses01', 'list,float#sep=|', ''), C('startPointTag', 'string#ref=Global/Tag.name', '')]
    rows = [{'trackId': 'racemap', 'lapsToFinish': rfm['lapsToFinish'], 'crossCooldownSeconds': lp['minimumCrossingCooldownSeconds'], 'maxProgressJumpWithoutCheckpoint01': lp['maximumProgressJumpWithoutCheckpoint01'],
             'minForwardDot': lp['minForwardDot'], 'armDistanceMeters': lp['minimumWorldDistanceFromStartToArmLap'], 'useSequentialCheckpoints': boolv(lp['useSequentialCheckpoints']),
             'autoAdvanceSplineCheckpoints': boolv(lp['autoAdvanceSplineCheckpoints']), 'requiredCheckpointCount': lp['requiredCheckpointCount'],
             'fallbackCheckpointProgresses01': '|'.join(fmt(x) for x in lp['fallbackCheckpointProgresses01']), 'startPointTag': lp['startPointTag']}]
    sheet(WB, 'LapRule', cols, rows, '(新增) LapRuleRecord ← LapProgress + RaceFinishManager', f'{BP}; {RM}', 'nearEnd/nearStart/minimumForwardWrapDelta 为死字段，未入表')

    rct = X.one(BP, 'RaceCompletionTracker')
    cols = [C('trackId', 'string#key#ref=Match/Track.trackId', '')] + [C(k, 'float', '') for k in ['wrongWayWrapStartThreshold', 'wrongWayWrapEndThreshold', 'wrongWayForwardDotThreshold',
            'wrongWayRecoveryLapProgressThreshold', 'wrongWayForceRespawnLapProgressThreshold', 'wrongWayRespawnLapProgress', 'wrongWayLap0RespawnLapProgress', 'startGraceSeconds']]
    sheet(WB, 'WrongWayRule', cols, [rowsof(rct, cols, {'trackId': 'racemap'})], '(新增) WrongWayRuleRecord ← RaceCompletionTracker', BP)

    cam = X.one(BP, 'PlayerCamera')
    cols = [C('profileId', 'string#key', '')] + [C(k, t, c) for k, t, c in [('directionalOffsetPerSpeed', 'vector3', ''), ('maxDirectionalOffset', 'vector3', ''), ('baseFieldOfView', 'float', '被 Cinemachine lens 覆盖，无效'),
            ('zoomBySpeed', 'bool', ''), ('speedForMaxZoomOut', 'float', '与 maxSpeed 80 耦合'), ('minSpeedFieldOfView', 'float', ''), ('maxSpeedFieldOfView', 'float', ''), ('maxSpeedDistanceOffset', 'float', ''),
            ('speedZoomSmoothTime', 'float', ''), ('adaptToPlayerScale', 'bool', ''), ('cameraDistanceScaleFactor', 'float', ''), ('cameraFovPerExtraScale', 'float', ''), ('offsetSmoothTime', 'float', ''),
            ('stableIntroExitSmoothTime', 'float', '代码默认'), ('stableIntroExitBlendDuration', 'float', '代码默认')]]
    sheet(WB, 'Camera', cols, [rowsof(cam, cols, {'profileId': 'default', 'stableIntroExitSmoothTime': cam.get('stableIntroExitSmoothTime', 0.6), 'stableIntroExitBlendDuration': cam.get('stableIntroExitBlendDuration', 1.2)})],
          '(新增) CameraRecord ← PlayerCamera', BP)

    sp = X.one(BP, 'BuddahSpeedParticleEffect')
    at = X.one(BP, 'PlayerAccelerationTrail'); bt = X.one(BP, 'PlayerBlackCurtainTrail'); om = X.one(BP, 'LocalPlayerOverheadMarker')
    cols = [C('profileId', 'string#key', ''), C('speedFxPlayThreshold', 'float', ''), C('speedFxStopThreshold', 'float', ''), C('speedFxUsePlanarSpeed', 'bool', ''),
            C('accelTrailColor', 'color', 'fallback'), C('accelTrailWidth', 'float', ''), C('accelTrailTime', 'float', ''), C('accelTrailFadeOutMultiplier', 'float', ''),
            C('curtainTrailColor', 'color', 'fallback（实际 TrailRenderer 为橙色）'), C('curtainTrailWidth', 'float', ''), C('curtainTrailTime', 'float', ''), C('curtainTrailFadeOutMultiplier', 'float', ''),
            C('overheadMarkerHeight', 'float', ''), C('overheadMarkerPixelHeight', 'float', ''), C('overheadMarkerMinScale', 'float', ''), C('overheadMarkerMaxScale', 'float', ''),
            C('localMarkerColor', 'color', 'LocalPlayerMarker_Cyan.mat'), C('localMarkerOutlineColor', 'color', '')]
    def matcol(p):
        try:
            m = X.first_doc(p, 'Material')
            for c in m['m_SavedProperties']['m_Colors']:
                if '_BaseColor' in c:
                    return color(c['_BaseColor'])
        except Exception:
            return ''
        return ''
    rows = [{'profileId': 'default', 'speedFxPlayThreshold': sp['playSpeedThreshold'], 'speedFxStopThreshold': sp['stopSpeedThreshold'], 'speedFxUsePlanarSpeed': boolv(sp['usePlanarSpeed']),
             'accelTrailColor': color(at['fallbackColor']), 'accelTrailWidth': at['fallbackWidth'], 'accelTrailTime': at['fallbackTime'], 'accelTrailFadeOutMultiplier': at['fadeOutMultiplier'],
             'curtainTrailColor': color(bt['fallbackColor']), 'curtainTrailWidth': bt['fallbackWidth'], 'curtainTrailTime': bt['fallbackTime'], 'curtainTrailFadeOutMultiplier': bt['fadeOutMultiplier'],
             'overheadMarkerHeight': om['heightAboveVisualRoot'], 'overheadMarkerPixelHeight': om['targetPixelHeight'], 'overheadMarkerMinScale': om['minimumScale'], 'overheadMarkerMaxScale': om['maximumScale'],
             'localMarkerColor': matcol('Assets/Placeholder/UI/LocalPlayerMarker_Cyan.mat'), 'localMarkerOutlineColor': matcol('Assets/Placeholder/UI/LocalPlayerMarker_Outline.mat')}]
    sheet(WB, 'RacerVisual', cols, rows, '(新增) RacerVisualRecord', f'{BP}; Assets/Placeholder/UI/*.mat')

    spt = X.one(BP, 'SplineProgressTracker'); ppr = X.one(BP, 'PlayerProgressReporter'); tsr = X.one(RM, 'TrackSplineRef')
    cols = [C('profileId', 'string#key', ''), C('jumpMetersThreshold', 'float', '', 'adv'), C('windowRadiusT', 'float', '', 'adv'), C('windowSteps', 'int', '', 'adv'), C('maxProjectionDistance', 'float', '', 'adv'),
            C('maxStepFactor', 'float', '', 'adv'), C('trackSplineSamples', 'int', 'TrackSplineRef.samples（场景）', 'adv'), C('progressReportIntervalSeconds', 'float', '网络上报间隔', 'adv')]
    sheet(WB, 'SplineTracking', cols, [rowsof(spt, cols, {'profileId': 'default', 'trackSplineSamples': tsr['samples'], 'progressReportIntervalSeconds': ppr['reportIntervalSeconds']})],
          '(新增, 工程向) SplineTrackingRecord', f'{BP}; {RM}')

# =====================================================================================
# AI.xlsx
# =====================================================================================
def build_ai():
    WB = 'AI'
    tiers = {d: X.mono_asset(f'Assets/Resources/AI/{d}.asset') for d in ['Easy', 'Normal', 'Hard']}
    fields = [k for k in tiers['Easy'].keys() if not k.startswith('m_') and k != 'NoiseSeed']
    adv = {'LateralDamping', 'PredictionGain', 'AttitudeDeadbandDegrees', 'AttitudeHysteresisDegrees', 'RolloutSeconds', 'RolloutHoldSeconds', 'ThrustReplanTicks', 'RolloutSubsteps', 'RolloutSampleTicks',
           'RolloutTail', 'SwitchPenaltyPerDegree', 'RolloutLateralWeight', 'RolloutLateralVelocityWeight', 'RolloutOverspeedWeight', 'RolloutUnderspeedWeight', 'RolloutProgressWeight', 'RolloutTerminalWeight',
           'RolloutCenterWeight', 'WallMargin', 'SpeedMargin', 'LookaheadSeconds', 'ThrustPaceFactor', 'PlanningBrakeAcceleration', 'WobbleTicks'}
    ints = {'ReactionTicks', 'ReactionJitterTicks', 'ThrustReplanTicks', 'RolloutSubsteps', 'RolloutSampleTicks', 'WobbleTicks'}
    cols = [C('difficulty', 'enum:SoloDifficulty#key', 'Easy / Normal / Hard')]
    for k in fields:
        t = 'bool' if k == 'UseWallCorridor' else 'enum:RolloutTailMode' if k == 'RolloutTail' else 'int' if k in ints else 'float'
        cols.append(C(k, t, '', 'adv' if k in adv else ''))
    rows = []
    for d, f in tiers.items():
        r = rowsof(f, cols, {'difficulty': d})
        if 'RolloutTail' in r:
            r['RolloutTail'] = {0: 'Anchor', 1: 'KeepCandidate', 2: 'FullThrust'}[f['RolloutTail']]
        rows.append(r)
    sheet(WB, 'DrivingTier', cols, rows, 'AIDifficultyProfile（字段名沿用 PascalCase）', 'Assets/Resources/AI/{Easy,Normal,Hard}.asset', 'adv 列为规划器高级参数；替代 Tools/ai/difficulty-*.json 生成链')

    cols = [C('key', 'string#key', ''), C('value', 'float', ''), C('source', 'string', '', 'doc'), C('comment', 'string', '', 'doc')]
    g = [('targetSpeedMin', 20, 'AIDifficultyProfiles.Resolve:40', '每车 TargetSpeed clamp 下限'), ('targetSpeedMax', 80, 'AIDifficultyProfiles.Resolve:40', '上限，与车辆 maxSpeed 80 耦合'),
         ('recoverySpeed', 5, 'ThrustVectorPlanner.cs:26', '低于此速度沿赛道全推'), ('recoveryVelocityErrorDegrees', 90, 'ThrustVectorPlanner.cs:26', '倒行判定'),
         ('sideSwitchLateral', 3, 'TVP:27', '[Eng]'), ('holdLateral', 18, 'TVP:27', '[Eng]'), ('topSpeed', 79, 'TVP:28', '[Eng] 与 maxSpeed 耦合'), ('topPace', 78, 'TVP:28', '[Eng]'),
         ('minBrakeAngleDegrees', 120, 'TVP:29', '[Eng]'), ('maxTargetDegrees', 178, 'TVP:29', '[Eng]'), ('attitudeAcceleration', 4.82, 'TVP:30', '[Eng] rad/s² 物理拟合'),
         ('minPredictionSeconds', 0.3, 'TVP:31', '[Eng]'), ('maxPredictionSeconds', 1, 'TVP:31', '[Eng]'), ('reanchorDegrees', 200, 'TVP:32', '[Eng]'), ('collisionReanchorDegrees', 180, 'TVP:32', '[Eng]'),
         ('fullTurnDegrees', 360, 'TVP:32', '[Eng]'), ('reprojectLateral', 15, 'TVP:33', '[Eng] m'), ('collisionMemorySeconds', 0.5, 'TVP:33', '[Eng]'), ('wobbleClampSigmas', 3, 'TVP:33', '[Eng]'),
         ('velocityHeadingMinSpeedSquared', 25, 'TVP:34', '[Eng]'), ('nearSearchSegments', 8, 'TVP:35', '[Eng]'), ('wideSearchSegments', 40, 'TVP:35', '[Eng]'), ('rolloutSearchSegments', 6, 'TVP:35', '[Eng]'),
         ('racingLineSampleMeters', 2, 'SplineRacingLine.Capture:75', '[Eng]'), ('racingLineMinSamples', 16, 'SplineRacingLine.Capture:77', '[Eng]'), ('curvatureChordMeters', 12, 'SplineRacingLine:113,149', '[Eng]'),
         ('windowPaceMeters', 80, 'SplineRacingLine:132', '[Eng] 与 maxSpeed 耦合'), ('wallProbeMaxDistance', 80, 'TrackWallProbe.cs:11', '[Eng]'), ('wallProbeHeight', 1.5, 'TrackWallProbe.cs:11', '[Eng]'),
         ('sideContactNormalY', 0.7, 'AIRacerDriver.cs:90', '[Eng]'), ('groundNormalY', 0.99, 'AIRacerDriver.cs:96', '[Eng]'), ('defaultFriction', 0.6, 'AIRacerDriver.cs:98', '[Eng]'), ('groundContactValiditySeconds', 0.1, 'AIRacerDriver.cs:142', '[Eng]')]
    sheet(WB, 'DrivingGlobal', cols, [{'key': a, 'value': b, 'source': c, 'comment': d} for a, b, c, d in g], '(新增, 键值) DrivingGlobal → GlobalRule 或独立记录', 'ThrustVectorPlanner.cs 等代码常量',
          '[Eng] 项为规划器内部常量，按用户要求一并入表')

    pj = X.json_file('Assets/Resources/AI/SkillPersonalities.json')
    diffs = pj['Difficulties']; dnames = ['Easy', 'Normal', 'Hard']
    dkeys = [k for k in diffs[0].keys()]
    cols = [C('difficulty', 'enum:SoloDifficulty#key', '')] + [C(k, 'int' if k in ('ImpairedReactionTicks', 'ImpairedDrivingReactionTicks') else 'float', '') for k in dkeys]
    sheet(WB, 'SkillDifficulty', cols, [{'difficulty': dnames[i], **{k: d[k] for k in dkeys}} for i, d in enumerate(diffs)], 'AISkillDifficulty', 'Assets/Resources/AI/SkillPersonalities.json')

    kinds = ['acceleration', 'slowtrap', 'giant', 'push_projectile_hands', 'reverseturn', 'blackcurtain']
    sits = ['Pursued', 'LeadingAlone', 'Chasing', 'Pack', 'TrailingAlone']
    targets = {0: 'Ahead', 1: 'Behind', 2: 'PackCentre', 3: 'EnteringCorner', 4: 'Impaired'}
    cols = [C('personalityId', 'string#key', ''), C('displayNameKey', 'string#ref=UI/Text.key', '产品中的 AI 显示名'), C('slot0SkillId', 'string#ref=Skill/SkillDefinition.skillId', ''),
            C('slot1SkillId', 'string#ref=Skill/SkillDefinition.skillId', ''), C('slot2SkillId', 'string#ref=Skill/SkillDefinition.skillId', '四键招'), C('patience', 'float#range=0,0.6', 'θ'),
            C('tempoSeconds', 'float#range=0.5,4', 'τ'), C('target', 'enum:AISkillTargetPreference', ''), C('impairedTargetMultiplier', 'float#range=1,2', ''), C('handsShots', 'int#range=0,6', '')]
    rows = []; wrows = []; brows = []
    for p in pj['Personalities']:
        pid = p['Id']
        k = T(f'ai.personality.{pid}.name', p['DisplayName'], '', 'AI 人格显示名', 'AISkillPersonality.DisplayName')
        lo = p['Loadout']
        rows.append({'personalityId': pid, 'displayNameKey': k, 'slot0SkillId': lo[0], 'slot1SkillId': lo[1], 'slot2SkillId': lo[2], 'patience': p['Patience'], 'tempoSeconds': p['TempoSeconds'],
                     'target': targets[p['Target']], 'impairedTargetMultiplier': p.get('ImpairedTargetMultiplier', 1), 'handsShots': p['HandsShots']})
        for i, w in enumerate(p['Weights']):
            wrows.append({'personalityId': pid, 'skillId': kinds[i], 'weight': w})
        for i, b in enumerate(p['SituationBias']):
            brows.append({'personalityId': pid, 'situation': sits[i], 'bias': b})
    sheet(WB, 'Personality', cols, rows, 'AISkillPersonality', 'SkillPersonalities.json', '人格数必须等于 Roster 行数（当前 5）')
    sheet(WB, 'PersonalitySkillWeight', [C('personalityId', 'string#key#ref=Personality.personalityId', ''), C('skillId', 'string#key#ref=Skill/SkillDefinition.skillId', ''), C('weight', 'float#range=0,1.5', '不在配装内的技能权重无效')],
          wrows, 'AISkillPersonality.Weights[6]（按 skillId 展开）', 'SkillPersonalities.json')
    sheet(WB, 'PersonalitySituationBias', [C('personalityId', 'string#key#ref=Personality.personalityId', ''), C('situation', 'enum:AISkillSituation#key', ''), C('bias', 'float#range=0,1.5', '')],
          brows, 'AISkillPersonality.SituationBias[5]', 'SkillPersonalities.json')

    tun = pj['Tuning']
    sev = tun['BacklashSeverity']
    cols = [C('skillId', 'string#key#ref=Skill/SkillDefinition.skillId', ''), C('backlashSeverity', 'float#range=0,1', ''), C('blockWhenGlobalVisible', 'bool', '全局效果可见时不叠加'),
            C('minStraightSeconds', 'float', 'Acceleration'), C('scoreBase', 'float', ''), C('scoreSlope', 'float', ''), C('maxValiditySeconds', 'float', ''), C('minValiditySeconds', 'float', ''),
            C('euclidMultiplier', 'float', '欧氏距离倍率'), C('maxYawDegrees', 'float', ''), C('yawPenalty', 'float', ''), C('scorePerContact', 'float', ''), C('packBonus', 'float', ''), C('source', 'string', '', 'doc')]
    opp = [('acceleration', sev[0], False, 3, '', '', '', '', '', '', '', '', '', 'AISkillDecision.cs:72-78'),
           ('slowtrap', sev[1], False, '', 0.3, 0.7, 6, '', '', '', '', '', '', 'DEC:93-96'),
           ('giant', sev[2], False, '', '', '', '', 2, 1.5, '', '', 0.5, '', 'DEC:98-105,131-132'),
           ('push_projectile_hands', sev[3], False, '', 0.35, 0.65, '', '', 1.5, 110, 0.5, '', '', 'DEC:106-114'),
           ('reverseturn', sev[4], True, '', '', '', '', 2, '', '', '', '', '', 'DEC:115-121,133-134'),
           ('blackcurtain', sev[5], True, '', '', '', '', '', '', '', '', '', 0.25, 'DEC:115-121,135-136')]
    keys = [c[0] for c in cols]
    sheet(WB, 'SkillOpportunity', cols, [dict(zip(keys, o)) for o in opp], '(新增) AISkillOpportunityRecord', 'SkillPersonalities.json Tuning.BacklashSeverity + AISkillDecision.cs 常量')

    cols = [C('key', 'string#key', ''), C('value', 'float', ''), C('range', 'string', '校验范围', 'doc'), C('source', 'string', '', 'doc')]
    rng = {'SnapshotSeconds': '0.05-0.5', 'SituationHysteresisSeconds': '0-5', 'NearDistance': '1-500', 'FarDistance': 'Near-2000', 'TrapDistance': '1-500', 'ContactDistance': '1-100', 'HandsDistance': '1-1800',
           'LaneWidth': '1-100', 'StraightCurvature': '0.00001-0.1', 'CornerCurvature': 'Straight-1', 'KeyMinSeconds': '0.05-0.3', 'KeyMaxSeconds': 'Min-0.34 (< combo_input_window)', 'AimHorizonSeconds': '0.1-5', 'SpeedDropFraction': '0.01-1'}
    rows = [{'key': k, 'value': v, 'range': rng.get(k, ''), 'source': 'SkillPersonalities.json Tuning'} for k, v in tun.items() if k != 'BacklashSeverity']
    rows += [{'key': k, 'value': v, 'range': '', 'source': s} for k, v, s in [
        ('speedDropImpairSeconds', 0.5, 'AISkillWorld.cs:78'), ('speedDropMinSpeed', 5, 'AISkillWorld.cs:78; AISkillDecision.cs:16'), ('straightLookaheadSeconds', 10, 'AISkillWorld.cs:93'),
        ('cornerLookaheadSeconds', 2, 'AISkillWorld.cs:95'), ('cornerValidityBase', 2, 'AISkillWorld.cs:96'), ('cornerValidityScale', 2, 'AISkillWorld.cs:96'),
        ('targetBonusImpaired', 2, 'AISkillDecision.TargetScore'), ('targetBonusAhead', 1, ''), ('targetBonusBehind', 1, ''), ('targetBonusPackCentrePerNeighbour', 0.25, ''),
        ('packMinNeighbours', 2, 'AISkillDecision.cs:59'), ('severeSeverityThreshold', 0.5, 'AISkillDecision.cs:146'), ('maxWindowSeconds', 6, 'AISkillDecision.cs:158'), ('minRelativeSpeed', 0.1, 'AISkillDecision.cs:158'),
        ('maxElapsedSeconds', 1, 'AISkillCaster.cs:159'), ('maxComboRetries', 1, 'AISkillCommitment.cs:38'), ('scaleShrunkThreshold', 0.99, 'AISkillWorld.cs:76'), ('trackTableCellMeters', 4, 'AISkillWorld.cs:167')]]
    sheet(WB, 'SkillGlobal', cols, rows, '(新增, 键值) AISkillTuning + 代码常量', 'SkillPersonalities.json; AISkillWorld/Decision/Caster/Commitment.cs')

    cols = [C('slotIndex', 'int#key', 'AI 槽位 0-4'), C('personalityId', 'string#ref=Personality.personalityId', '按 JSON 顺序隐式分配'), C('displayNameOverrideKey', 'string#ref=UI/Text.key', '空=用人格名')]
    sheet(WB, 'Roster', cols, [{'slotIndex': i, 'personalityId': p['Id'], 'displayNameOverrideKey': ''} for i, p in enumerate(pj['Personalities'])], '(新增) AIRosterRecord', 'MatchSpawnManager.SpawnSoloAI:165')

# =====================================================================================
# Match.xlsx
# =====================================================================================
def build_match():
    WB = 'Match'
    rsm = X.one(RSM, 'RoomStateManager'); psm = X.one(PS, 'PropertiesSelectionManager'); rfm = X.one(RM, 'RaceFinishManager'); rdm = X.one(RM, 'ResultDecisionManager')
    cols = [C('modeId', 'enum:MatchMode#key', 'Online / Solo / Practice'), C('autoStartRoom', 'bool', ''), C('requiresReady', 'bool', '实际由 RoomStateManager._requireAllClientsReady 决定'),
            C('selectionTimeoutEnabled', 'bool', ''), C('skipMapVote', 'bool', ''), C('voteOnResults', 'bool', ''), C('resultDecisionTimeoutEnabled', 'bool', ''), C('returnTarget', 'enum:ReturnTarget', ''),
            C('endWhenAllRacersFinished', 'bool', ''), C('endOnHumanFinish', 'bool', ''), C('allowSkipSpectating', 'bool', '占位'), C('allowQuitDialog', 'bool', ''), C('allowPause', 'bool', '占位'),
            C('showRacerNameTags', 'bool', '占位'), C('showOpponentsOnMinimap', 'bool', '占位'), C('defaultPlayerNameKey', 'string#ref=UI/Text.key', ''),
            C('pregameCountdownSeconds', 'int', 'RoomStateManager prefab'), C('selectionStageSeconds', 'int', 'PropertiesSelectionManager 场景'), C('postFirstFinishCountdownSeconds', 'float', 'RaceFinishManager 场景'),
            C('resultDecisionSeconds', 'int', 'ResultDecisionManager 场景'), C('minPlayersToStart', 'int', 'CanHostStartGame'), C('hostAutoReady', 'bool', ''), C('aiCount', 'int#range=0,5', 'Solo 固定 5')]
    pk = T('match.default_player_name', '玩家', 'Player', 'Solo 默认玩家名', 'SoloMatchRules.DefaultPlayerName')
    common = {'pregameCountdownSeconds': rsm['_pregameCountdownSeconds'], 'selectionStageSeconds': psm['_stageDurationSeconds'], 'postFirstFinishCountdownSeconds': rfm['postFirstFinishCountdownSeconds'],
              'resultDecisionSeconds': rdm['decisionDurationSeconds'], 'minPlayersToStart': 1, 'hostAutoReady': True}
    online = {'modeId': 'Online', 'autoStartRoom': False, 'requiresReady': boolv(rsm['_requireAllClientsReady']), 'selectionTimeoutEnabled': True, 'skipMapVote': False, 'voteOnResults': True, 'resultDecisionTimeoutEnabled': True,
              'returnTarget': 'Room', 'endWhenAllRacersFinished': False, 'endOnHumanFinish': False, 'allowSkipSpectating': False, 'allowQuitDialog': False, 'allowPause': False, 'showRacerNameTags': False,
              'showOpponentsOnMinimap': False, 'defaultPlayerNameKey': '', 'aiCount': 0, **common}
    solo = {**online, 'modeId': 'Solo', 'autoStartRoom': True, 'requiresReady': False, 'selectionTimeoutEnabled': False, 'skipMapVote': True, 'voteOnResults': False, 'resultDecisionTimeoutEnabled': False,
            'returnTarget': 'MainMenuHome', 'endWhenAllRacersFinished': True, 'allowQuitDialog': True, 'defaultPlayerNameKey': pk, 'aiCount': 5}
    practice = {**solo, 'modeId': 'Practice', 'endOnHumanFinish': True, 'aiCount': 0}
    sheet(WB, 'ModeRule', cols, [online, solo, practice], '(新增) MatchModeRuleRecord ← IMatchRules 实现 + 各 manager 时长', 'OnlineMatchRules.cs; SoloMatchRules.cs; RoomStateManager.prefab; PropertySelection.unity; RaceMap.unity',
          'Practice 行 = Solo 且 aiCount=0，目前 UI 无入口')

    mm = X.one(UIP, 'MiniMapController')
    rra = X.one(RM, 'RaceResultAreaManager')
    cols = [C('trackId', 'string#key', ''), C('sceneName', 'string#ref=Global/Scene.sceneName', ''), C('lapsToFinish', 'int', '同 Movement/LapRule'), C('resultOverflowOffset', 'vector3', ''), C('missingPlayerOffset', 'vector3', ''),
            C('minimapSpritePath', 'string#path', ''), C('minimapWorldMinX', 'float', ''), C('minimapWorldMaxX', 'float', ''), C('minimapWorldMinZ', 'float', ''), C('minimapWorldMaxZ', 'float', ''),
            C('minimapImageWidth', 'float', 'RaceMap 覆盖值'), C('trackSplineSamples', 'int', '', 'adv')]
    rows = [{'trackId': 'racemap', 'sceneName': 'RaceMap', 'lapsToFinish': rfm['lapsToFinish'], 'resultOverflowOffset': vec(rra['overflowOffset']), 'missingPlayerOffset': vec(rra['missingPlayerOffset']),
             'minimapSpritePath': X.sprite_of_image(UIP, (mm.get('mapImage') or {}).get('fileID', 0)), 'minimapWorldMinX': mm['worldMinX'], 'minimapWorldMaxX': mm['worldMaxX'], 'minimapWorldMinZ': mm['worldMinZ'], 'minimapWorldMaxZ': mm['worldMaxZ'],
             'minimapImageWidth': 1120, 'trackSplineSamples': X.one(RM, 'TrackSplineRef')['samples']}]
    sheet(WB, 'Track', cols, rows, '(新增) TrackRecord', f'{RM}; {UIP} MiniMapController')

    def placed(cls, keyname, rel=RM):
        out = []
        for c in X.comps(rel, cls):
            pos, yaw, scale = X.world_transform(rel, c['goId'])
            out.append((c, pos, yaw))
        return out
    sm = X.one(RM, 'MatchSpawnManager')
    order = [(r.get('fileID')) for r in sm['_spawnPoints']]
    spawn = {c['fileId']: (c, p, y) for c, p, y in placed('MatchSpawnPoint', '')}
    rows = []
    for i, fid in enumerate(order):
        c, p, y = spawn[fid]
        rows.append({'trackId': 'racemap', 'order': i, 'objectName': c['go'], 'position': vec({'x': p[0], 'y': p[1], 'z': p[2]}), 'rotationY': round(y, 3)})
    sheet(WB, 'TrackSpawn', [C('trackId', 'string#key#ref=Track.trackId', ''), C('order', 'int#key', '人类按已生成人数取索引，AI 用 index+1'), C('objectName', 'string', '', 'doc'), C('position', 'vector3', '世界坐标'), C('rotationY', 'float', '')],
          rows, '(新增) TrackSpawnRecord ← MatchSpawnManager._spawnPoints', RM)
    rows = [{'trackId': 'racemap', 'checkpointId': c['f']['checkpointId'], 'objectName': c['go'], 'position': vec({'x': p[0], 'y': p[1], 'z': p[2]}), 'rotationY': round(y, 3),
             'size': vec((X.by_fileid(RM, [d['fileId'] for d in X.docs(RM) if d['kind'] == 'BoxCollider' and (d['data'].get('m_GameObject') or {}).get('fileID') == c['goId']][0])['data']['m_Size']))}
            for c, p, y in sorted(placed('Checkpoint', ''), key=lambda t: t[0]['f']['checkpointId'])]
    sheet(WB, 'TrackCheckpoint', [C('trackId', 'string#key#ref=Track.trackId', ''), C('checkpointId', 'int#key', ''), C('objectName', 'string', '', 'doc'), C('position', 'vector3', '世界坐标（父物体 CheckPoints 有偏移）'), C('rotationY', 'float', ''), C('size', 'vector3', 'BoxCollider size（未乘 scale）')],
          rows, '(新增) TrackCheckpointRecord ← Checkpoint', RM)
    rows = [{'trackId': 'racemap', 'rank': c['f']['spawnOrderIndex'] + 1, 'objectName': c['go'], 'position': vec({'x': p[0], 'y': p[1], 'z': p[2]}), 'rotationY': round(y, 3)}
            for c, p, y in sorted(placed('EndFieldSpawnPoint', ''), key=lambda t: t[0]['f']['spawnOrderIndex'])]
    sheet(WB, 'TrackResultAnchor', [C('trackId', 'string#key#ref=Track.trackId', ''), C('rank', 'int#key', '名次 1 起'), C('objectName', 'string', '', 'doc'), C('position', 'vector3', ''), C('rotationY', 'float', '')],
          rows, '(新增) TrackResultAnchorRecord ← EndFieldSpawnPoint', RM)

    slm = X.one(MM, 'SteamLobbyManager')
    cols = [C('profileId', 'string#key', ''), C('maxPlayers', 'int', ''), C('minPlayers', 'int', ''), C('defaultLobbyNameKey', 'string#ref=UI/Text.key', ''), C('defaultVisibility', 'enum:LobbyVisibility', ''),
            C('maxSearchResults', 'int', ''), C('clientConnectTimeoutSeconds', 'float', 'SessionLauncher.cs:66'), C('hostStartTimeoutSeconds', 'float', 'SessionLauncher.cs:81'), C('maxPlayerNameLength', 'int', 'RoomStateManager.cs:22'),
            C('fishnetMaximumClients', 'int', 'ServerManager', 'adv')]
    lk = T('lobby.default_room_name', slm['_defaultLobbyName'], slm['_defaultLobbyName'], '默认房间名', 'SteamLobbyManager._defaultLobbyName')
    rows = [{'profileId': 'default', 'maxPlayers': slm['_defaultMaxPlayers'], 'minPlayers': 2, 'defaultLobbyNameKey': lk, 'defaultVisibility': 'Public', 'maxSearchResults': slm['_maxSearchResults'],
             'clientConnectTimeoutSeconds': 30, 'hostStartTimeoutSeconds': 20, 'maxPlayerNameLength': 32, 'fishnetMaximumClients': 6}]
    sheet(WB, 'Lobby', cols, rows, '(新增) LobbyRecord', f'{MM} SteamLobbyManager; SessionLauncher.cs')

    cols = [C('modeId', 'enum:MatchMode#key', ''), C('vetoChoice', 'enum:ResultPlayerChoice', '任一玩家选它立即生效'), C('unanimousChoice', 'enum:ResultPlayerChoice', '全员一致才生效'), C('timeoutChoice', 'enum:ResultPlayerChoice', ''), C('disconnectChoice', 'enum:ResultPlayerChoice', '')]
    sheet(WB, 'ResultVoteRule', cols, [{'modeId': 'Online', 'vetoChoice': 'ReturnToRoom', 'unanimousChoice': 'StartNewGame', 'timeoutChoice': 'ReturnToRoom', 'disconnectChoice': 'ReturnToRoom'}],
          '(新增) ResultVoteRuleRecord', 'ResultDecisionManager.EvaluateDecisionServer:252-275')

    mrp = X.one(RM, 'MatchResultPresentationCoordinator'); rtb = X.one(RM, 'ResultPresentationTimelineBridge'); lfb = X.one(RM, 'LocalFinishPresentationTimelineBridge')
    cols = [C('profileId', 'string#key', ''), C('personalFinishDissolveOutSeconds', 'float', ''), C('forcedFinishDissolveOutSeconds', 'float', ''), C('resultAreaDissolveInSeconds', 'float', ''),
            C('allowMovementInResultArea', 'bool', ''), C('allowSkillsInResultArea', 'bool', ''), C('useFallbackTimelineDelays', 'bool', '为 false 时下列 4 项不生效'), C('blackScreenStartDelaySeconds', 'float', ''),
            C('blackScreenFullyCoveredDelaySeconds', 'float', ''), C('blackScreenEndDelaySeconds', 'float', ''), C('timelineFinishedDelaySeconds', 'float', ''), C('keepSharedCameraDuringResultArea', 'bool', ''),
            C('localFinishRewindBeforePlay', 'bool', ''), C('localFinishStopOnResultAreaInteractive', 'bool', ''), C('localFinishStopOnRaceReset', 'bool', '')]
    rows = [rowsof({**mrp, **rtb}, cols, {'profileId': 'default', 'localFinishRewindBeforePlay': boolv(lfb['rewindBeforePlay']), 'localFinishStopOnResultAreaInteractive': boolv(lfb['stopOnResultAreaInteractive']),
                                           'localFinishStopOnRaceReset': boolv(lfb['stopOnRaceReset'])})]
    sheet(WB, 'ResultPresentation', cols, rows, '(新增) ResultPresentationRecord', f'{RM}')

    cols = [C('stageKey', 'string#key', 'map / skill_loadout / skin'), C('order', 'int', ''), C('displayNameKey', 'string#ref=UI/Text.key', ''), C('selectionMode', 'enum:PropertySelectionMode', 'Vote / HostOnly / Single / Multi'),
            C('enabledOnline', 'bool', ''), C('enabledSolo', 'bool', ''), C('layoutMode', 'enum:SelectionLayout', 'Carousel / SkillWall'), C('autoFillOnTimeout', 'bool', '')]
    st = [('map', 0, T('selection.stage.map', 'Map Selection', 'Map Selection', '', 'PropertiesSelectionManager:1125'), 'Vote', True, False, 'Carousel', False),
          ('skill_loadout', 1, T('selection.stage.skill_loadout', 'Skill Loadout Selection', 'Skill Loadout Selection', '', ':1082'), 'Multi', True, True, 'SkillWall', True),
          ('skin', 2, T('selection.stage.skin', 'Skin Selection', 'Skin Selection', '', ':1133'), 'Single', True, True, 'Carousel', False)]
    sheet(WB, 'SelectionStage', cols, [dict(zip([c[0] for c in cols], s)) for s in st], '(新增) SelectionStageRecord', 'PropertiesSelectionManager.RegisterDefaultStageProperties:566-568', '地图/皮肤结果目前无人读取')
    cols = [C('stageKey', 'string#key#ref=SelectionStage.stageKey', ''), C('optionId', 'string#key', ''), C('displayNameKey', 'string#ref=UI/Text.key', ''), C('descriptionKey', 'string#ref=UI/Text.key', ''), C('iconKey', 'string', ''), C('unlocked', 'bool', '')]
    rows = []
    for s, ids in [('map', ['Map_A', 'Map_B', 'Map_C']), ('skin', ['Skin_A', 'Skin_B', 'Skin_C'])]:
        for oid in ids:
            n = oid.replace('_', ' ')
            rows.append({'stageKey': s, 'optionId': oid, 'displayNameKey': T(f'selection.option.{oid.lower()}.name', n, n, '占位', ''),
                         'descriptionKey': T(f'selection.option.{oid.lower()}.desc', f'Default placeholder {s} {oid[-1]}.', f'Default placeholder {s} {oid[-1]}.', '占位', ''), 'iconKey': '', 'unlocked': True})
    sheet(WB, 'SelectionOption', cols, rows, '(新增) SelectionOptionRecord', 'PropertiesSelectionManager:1126-1136（占位数据）')

    ism = X.one(RM, 'IntroSequenceManager')
    cols = [C('profileId', 'string#key', ''), C('fallbackIntroDurationSeconds', 'float', ''), C('introSpeedMetersPerSecond', 'float', ''), C('handoffLeadSeconds', 'float', '与 Movement/LaunchHandoff 同义'),
            C('introLeadInSeconds', 'float', ''), C('randomizeSlots', 'bool', 'Solo 强制 false'), C('deterministicShuffleSeed', 'int', '固定种子=每局相同'), C('timelinePath', 'string#path', 'Max(timeline, 样条÷速度)', 'doc'),
            C('timelineDurationSeconds', 'float', '由 timeline 资产导出，只读', 'doc')]
    tl = guid_path(ism.get('timelineAsset') or {})
    try:
        pd = X.one(RM, 'PlayableDirector')
    except Exception:
        pd = None
    tlpath = 'Assets/Animation/CG/TransitionToRaceMap.playable'
    rows = [{'profileId': 'default', 'fallbackIntroDurationSeconds': ism['fallbackIntroDuration'], 'introSpeedMetersPerSecond': ism['introSpeedMetersPerSecond'], 'handoffLeadSeconds': ism['handoffLeadSeconds'],
             'introLeadInSeconds': ism['introLeadInSeconds'], 'randomizeSlots': boolv(ism['randomizeSlots']), 'deterministicShuffleSeed': ism['deterministicShuffleSeed'], 'timelinePath': tlpath,
             'timelineDurationSeconds': X.playable_duration(tlpath)}]
    sheet(WB, 'Intro', cols, rows, '(新增) IntroRecord ← IntroSequenceManager', RM)
    rows = []
    for lay in ism.get('layouts', []):
        pc = lay.get('playerCount')
        slots = []
        for s in lay.get('slots', []):
            fid = s.get('fileID', 0); g = s.get('guid')
            if g:
                prel = guid_path(s)
                for c in X.comps(prel, 'IntroSlot'):
                    slots.append(f"{os.path.basename(prel)}:{c['go']}")
            elif fid:
                slots.append(X.resolve_ref_name(RM, fid))
        rows.append({'playerCount': pc, 'layoutName': lay.get('layoutName') or lay.get('name') or '', 'slots': '|'.join(slots), 'isPlaceholder': 'Placeholder' in str(lay)})
    sheet(WB, 'IntroLayout', [C('playerCount', 'int#key', ''), C('layoutName', 'string', ''), C('slots', 'list,string#sep=|', '槽位物体（场景或 Placeholder prefab）'), C('isPlaceholder', 'bool', '', 'doc')],
          rows, '(新增) IntroLayoutRecord ← IntroSequenceManager.layouts', RM, '4P/5P/6P 为占位 prefab；Solo 永远用 6P')
    slot = X.comps('Assets/Placeholder/Intro/Layout4P.prefab', 'IntroSlot')[0]['f']
    vs = slot.get('visualStyle', {})
    cols = [C('styleId', 'string#key', ''), C('yawScale', 'float', ''), C('rollScale', 'float', ''), C('swayAmplitude', 'float', ''), C('swayFrequency', 'float', ''), C('arcLengthSamples', 'int', 'SplineIntroPath', 'adv')]
    sheet(WB, 'IntroSlotStyle', cols, [{'styleId': 'default', **{k: vs.get(k, '') for k in ['yawScale', 'rollScale', 'swayAmplitude', 'swayFrequency']},
                                        'arcLengthSamples': X.comps('Assets/Placeholder/Intro/Layout4P.prefab', 'SplineIntroPath')[0]['f']['arcLengthSamples']}],
          '(新增) IntroSlotStyleRecord（范围内未找到消费方）', 'Assets/Placeholder/Intro/Layout4P.prefab')

    cols = [C('key', 'string#key', ''), C('value', 'string', ''), C('source', 'string', '', 'doc')]
    rows = [{'key': 'aiFallbackNamePrefixKey', 'value': T('racer.ai_fallback_prefix', 'AI ', 'AI ', 'AI 名为空时前缀+序号', 'RacerIdentity.cs:23'), 'source': 'RacerIdentity.cs:23'},
            {'key': 'humanFallbackNameKey', 'value': T('racer.human_fallback', 'Player', 'Player', '', 'RacerIdentity.cs:23'), 'source': 'RacerIdentity.cs:23'},
            {'key': 'onlineHumanNamePrefixKey', 'value': T('racer.online_human_prefix', 'Player ', 'Player ', '后接 clientId', 'MatchSpawnManager.cs:135'), 'source': 'MatchSpawnManager.cs:135'},
            {'key': 'aiGameObjectNamePrefix', 'value': 'AI Racer ', 'source': 'MatchSpawnManager.cs:167（非显示）'},
            {'key': 'defaultDifficulty', 'value': 'Normal', 'source': 'SoloSetupPanel.cs:23'}]
    sheet(WB, 'RacerNaming', cols, rows, '(新增, 键值) RacerNaming', 'RacerIdentity.cs; MatchSpawnManager.cs; SoloSetupPanel.cs')

# =====================================================================================
# UI.xlsx
# =====================================================================================
def build_ui():
    WB = 'UI'
    # --- SkillIcon ---
    lib = X.one(UIP, 'SkillUiSpriteLibrary')
    cols = [C('skillId', 'string#key#ref=Skill/SkillDefinition.skillId', ''), C('hudIdleSpritePath', 'string#path', 'black'), C('hudActiveSpritePath', 'string#path', 'white'), C('previewSpritePath', 'string#path', '选择界面（未实现）')]
    rows = [{'skillId': e['skillId'], 'hudIdleSpritePath': guid_path(e.get('blackSprite') or {}), 'hudActiveSpritePath': guid_path(e.get('whiteSprite') or {}), 'previewSpritePath': ''} for e in lib['entries']]
    sheet(WB, 'SkillIcon', cols, rows, '(新增) SkillIconRecord ← SkillUiSpriteLibrary', UIP, '*_anti 无图标；配置 iconKey 列未使用')

    # --- MenuOption (dropdowns) ---
    dd = X.dropdown_options(MM)
    cols = [C('optionGroup', 'string#key', ''), C('index', 'int#key', ''), C('value', 'string', ''), C('labelKey', 'string#ref=Text.key', ''), C('isDefault', 'bool', '')]
    rows = []
    for gname, grp in [('max_players', 'Max Player'), ('visibility', 'Room Visibility')]:
        cand = [k for k in dd if k and (grp.lower().replace(' ', '') in k.lower().replace(' ', '') or gname.replace('_', '') in k.lower())]
        if not cand:
            continue
        d = dd[cand[0]]
        for i, o in enumerate(d['options']):
            rows.append({'optionGroup': gname, 'index': i, 'value': o, 'labelKey': T(f'menu.option.{gname}.{str(o).lower()}', o, o, '', cand[0]), 'isDefault': i == d['value']})
    sheet(WB, 'MenuOption', cols, rows, '(新增) MenuOptionRecord', f'{MM} TMP_Dropdown m_Options', '代码兜底 CreateRoomUI.cs:131-167')

    # --- HudObsession ---
    ou = X.one(UIP, 'ObsessionUI')
    cols = [C('profileId', 'string#key', ''), C('smoothSpeed', 'float', ''), C('colorSmoothSpeed', 'float', ''), C('fadeSpeed', 'float', ''), C('maxShakeStrength', 'float', ''), C('shakeSpeed', 'float', ''),
            C('flashColor', 'color', ''), C('normalColor', 'color', ''), C('targetIconColor', 'color', ''), C('lowThresholdRatio', 'float', '代码 Max/3'), C('highThresholdRatio', 'float', '代码 2·Max/3'),
            C('flashFrequencyMin', 'float', 'ObsessionUI.cs:160'), C('flashFrequencyMax', 'float', ''), C('tierSprite0Path', 'string#path', ''), C('tierSprite1Path', 'string#path', ''), C('tierSprite2Path', 'string#path', '')]
    rows = [rowsof(ou, cols, {'profileId': 'default', 'lowThresholdRatio': 1/3, 'highThresholdRatio': 2/3, 'flashFrequencyMin': 2, 'flashFrequencyMax': 12,
                              'tierSprite0Path': guid_path(ou.get('buddha0_333') or {}), 'tierSprite1Path': guid_path(ou.get('buddha334_666') or {}), 'tierSprite2Path': guid_path(ou.get('buddha667_1000') or {})})]
    sheet(WB, 'HudObsession', cols, rows, '(新增) HudObsessionRecord ← ObsessionUI', UIP)

    # --- UiTiming (key-value across components) ---
    cols = [C('componentKey', 'string#key', '组件'), C('field', 'string#key', ''), C('value', 'string', ''), C('valueType', 'string', '', 'doc'), C('source', 'string', '', 'doc')]
    rows = []
    def kv(comp, f, src, fields, types=None):
        for k in fields:
            if k in f:
                v = f[k]
                t = (types or {}).get(k, 'float')
                sv = curve(v) if t == 'curve' else color(v) if t == 'color' else ('true' if boolv(v) else 'false') if t == 'bool' else fmt(v)
                rows.append({'componentKey': comp, 'field': k, 'value': sv, 'valueType': t, 'source': src})
    ss = X.comps('Assets/UI/UI预制键位/SkillSlot_1.prefab', 'SkillSlotUI')[0]['f']
    kv('SkillSlotUI', ss, 'Assets/UI/UI预制键位/SkillSlot_1.prefab', ['rotateSpeed', 'bindRetryInterval', 'successHoldSeconds', 'fallbackCastLockSeconds', 'previewTimeoutSeconds'])
    kv('SkillInspectPanelUI', X.one(PS, 'SkillInspectPanelUI'), PS, ['fadeDuration', 'fadeCurve'], {'fadeCurve': 'curve'})
    kv('SkillInspectController', X.one(PS, 'SkillInspectController'), PS, ['enterDuration', 'exitDuration', 'flightArcHeight', 'moveCurve', 'enableIdleMotion', 'idleAmplitude', 'idleFrequency', 'idleYawDegrees'], {'moveCurve': 'curve', 'enableIdleMotion': 'bool'})
    kv('SkillWallItemView', X.comps('Assets/UIMenus/PropertySelector/技能绘马.prefab', 'SkillWallItemView')[0]['f'], 'Assets/UIMenus/PropertySelector/技能绘马.prefab',
       ['hoverForwardDistance', 'hoverMoveDuration', 'defaultFlightDuration', 'normalColor', 'hoverColor', 'selectedColor'], {'normalColor': 'color', 'hoverColor': 'color', 'selectedColor': 'color'})
    kv('SkillWallSelectionController', X.one(PS, 'SkillWallSelectionController'), PS, ['wallColumnCount', 'wallHorizontalSpacing', 'wallVerticalSpacing', 'moveDuration', 'moveArcHeight', 'animateSelections'], {'wallColumnCount': 'int', 'animateSelections': 'bool'})
    kv('PropertyCarouselView', X.one('Assets/UIMenus/PropertySelector/PropertyGroupView.prefab', 'PropertyCarouselView'), 'Assets/UIMenus/PropertySelector/PropertyGroupView.prefab',
       ['spacing', 'itemYOffset', 'animationDuration', 'animationSharpness', 'minScale', 'maxScale', 'scaleFalloff'])
    kv('PropertySelectionStageSceneController', X.one(PS, 'PropertySelectionStageSceneController'), PS, ['transitionDuration', 'transitionCurve'], {'transitionCurve': 'curve'})
    kv('PropertySelectionMatchTransitionTimeline', X.one(PS, 'PropertySelectionMatchTransitionTimeline'), PS, ['fallbackDurationSeconds', 'alphaCurve'], {'alphaCurve': 'curve'})
    kv('PropertiesSelectionManager', X.one(PS, 'PropertiesSelectionManager'), PS, ['_sceneTransitionFadeDurationSeconds', '_preferTimelineForMatchTransition'], {'_preferTimelineForMatchTransition': 'bool'})
    kv('LeaderboardTMPUI', X.one(RM, 'LeaderboardTMPUI'), RM, ['localRefreshIntervalSeconds', 'maxRows'], {'maxRows': 'int'})
    kv('LeaderboardManager', X.one(RM, 'LeaderboardManager'), RM, ['refreshIntervalSeconds'])
    for c in X.comps(UIP, 'LapAddLineUITrigger'):
        kv(f"LapAddLineUITrigger@{c['go']}", c['f'], UIP, ['rebindIntervalSeconds'])
    rows.append({'componentKey': 'SceneFadeController', 'field': 'fallbackDurationSeconds', 'value': '1', 'valueType': 'float', 'source': 'SceneFadeController.cs:28（不在场景，代码默认）'})
    rows.append({'componentKey': 'PropertySelectionEnd.playable', 'field': 'durationSeconds', 'value': fmt(X.playable_duration('Assets/Animation/CG/PropertySelectionEnd.playable')), 'valueType': 'float', 'source': '真正生效的选择→比赛过场时长（timeline 导出，只读）'})
    rows.append({'componentKey': 'SoloResultsView', 'field': 'timeFormat', 'value': 'mm:ss.fff', 'valueType': 'string', 'source': 'SoloResultsView.cs:129-134'})
    rows.append({'componentKey': 'SoloResultsView', 'field': 'invalidTimeText', 'value': '--:--.---', 'valueType': 'string', 'source': 'SoloResultsView.cs:129-134'})
    sheet(WB, 'UiTiming', cols, rows, '(新增, 键值) UiTimingRecord', '各 UI 组件 SerializeField 实际值')

    # --- MiniMap (non-track part) ---
    mm = X.one(UIP, 'MiniMapController')
    cols = [C('profileId', 'string#key', ''), C('movementSensitivity', 'float', ''), C('clampToBounds', 'bool', ''), C('rebindEveryNFrames', 'int', '', 'adv'), C('staticFramesBeforeRebind', 'int', '', 'adv')]
    sheet(WB, 'MiniMap', cols, [rowsof(mm, cols, {'profileId': 'default'})], '(新增) MiniMapRecord（边界在 Match/Track）', UIP)

    # --- Text: hardcoded C# literals + scene static labels ---
    cs_files = sorted(set([p.replace(os.sep, '/') for p in
        [os.path.relpath(f, ROOT) for f in __import__('glob').glob(os.path.join(ROOT, 'Assets/Scripts/UI/**/*.cs'), recursive=True)] +
        [os.path.relpath(f, ROOT) for f in __import__('glob').glob(os.path.join(ROOT, 'Assets/Scripts/Match/UI/*.cs'))] +
        ['Assets/Scripts/Match/SessionLauncher.cs', 'Assets/Scripts/Network/Lobby/SteamLobbyManager.cs', 'Assets/Scripts/Network/Room/RoomRoster.cs',
         'Assets/Scripts/Network/Session/PropertySelection/PropertiesSelectionManager.cs', 'Assets/Scripts/Network/Gameplay/LeaderboardManager.cs',
         'Assets/Scripts/Network/Session/Results/ResultDecisionManager.cs', 'Assets/Scripts/Match/Contracts/RacerIdentity.cs']]))
    skip_classes = {'SkillUiSpriteLibrary', 'MiniMapController', 'InputSystem_Actions'}
    seen = set()
    for rel in cs_files:
        cls = os.path.splitext(os.path.basename(rel))[0]
        if cls in skip_classes:
            continue
        for line, lit in X.cs_string_literals(rel):
            if lit in seen:
                continue
            seen.add(lit)
            slug = re.sub(r'[^a-z0-9一-鿿]+', '_', lit.lower()).strip('_')[:40] or f'l{line}'
            key = f'ui.{cls.lower()}.{slug}'
            n = 2
            while key in text_rows:
                key = f'ui.{cls.lower()}.{slug}_{n}'; n += 1
            zh = lit if re.search(r'[一-鿿]', lit) else ''
            en = '' if zh else lit
            T(key, zh or en, en, '自动提取，待策划定稿 key', f'{rel}:{line}')
    for rel in [MM, PS, RM, UIP, 'Assets/UIMenus/PropertySelector/PropertyGroupView.prefab', 'Assets/UIMenus/Lobby/LobbyBrowserListItem.prefab']:
        for go, txt in X.tmp_texts(rel):
            txt = str(txt) if txt is not None else ''
            if not txt or txt.strip() in ('New Text', 'NewText', '​') or txt in seen:
                continue
            seen.add(txt)
            slug = re.sub(r'[^a-z0-9一-鿿]+', '_', txt.lower()).strip('_')[:40]
            key = f'ui.static.{slug}'
            n = 2
            while key in text_rows:
                key = f'ui.static.{slug}_{n}'; n += 1
            zh = txt if re.search(r'[一-鿿]', txt) else ''
            T(key, zh or txt, '' if zh else txt, '场景/prefab 静态文本', f'{rel} @{go}')
    cols = [C('key', 'string#key', 'domain.entity.field'), C('zh', 'text', '简体中文'), C('en', 'text', '英文'), C('comment', 'string', '', 'doc'), C('context', 'string', '来源', 'doc')]
    rows = [{'key': k, 'zh': v[0], 'en': v[1], 'comment': v[2], 'context': v[3]} for k, v in text_rows.items()]
    sheet(WB, 'Text', cols, rows, '(新增) LocalizedTextRecord', 'MAIN 技能文案 + C# 硬编码文案（自动提取）+ 场景静态文本', '自动提取的 key 需人工定稿；英文列为空表示待翻译')

# =====================================================================================
# Global.xlsx
# =====================================================================================
def build_global():
    WB = 'Global'
    cols = [C('ruleKey', 'string#key', 'snake_case'), C('ruleSetId', 'string#key', '空=default'), C('ruleValue', 'string', ''), C('description', 'string', ''), C('valueType', 'string', '校验用', 'doc'), C('consumer', 'string', '读取方', 'doc')]
    # v1 keys: the combo pair and the cast-confirm pair are rebuilt here and moved by the v2 layout pass (ComboInput / Skill).
    combo = X.one(BP, 'ComboSkillInput')
    known = {'combo_input_window_seconds': ('float', '搓招输入间隔窗口', 'ComboSkillInput.stepWindowSeconds'), 'combo_max_buffer': ('int', '搓招输入缓冲长度', 'ComboSkillInput.maxBuffer（prefab）'),
             'anti_cast_confirm_delay_seconds': ('float', '反噬施法前摇', 'SkillExecutor.cs:52'),
             'cast_confirm_delay_seconds': ('float', '普通技能施法前摇默认值；SkillBalance.castConfirmDelaySeconds 逐技能覆盖', 'SkillExecutor.ResolveCastConfirmDelay / ConfirmationSeconds'),
             'target_frame_rate': ('int', '须为 tick_rate 整数倍', 'FrameRateLock.cs:9')}
    values = {'combo_input_window_seconds': fmt(combo['stepWindowSeconds']), 'combo_max_buffer': fmt(combo['maxBuffer']), 'anti_cast_confirm_delay_seconds': '1', 'cast_confirm_delay_seconds': '1', 'target_frame_rate': '120'}
    for r in main['globalRules']:
        values[r['ruleKey']] = fmt(r['ruleValue'])
        if r['ruleKey'] not in known:
            known[r['ruleKey']] = ('float', r.get('description', ''), '')
    rows = [{'ruleKey': k, 'ruleSetId': 'default', 'ruleValue': values[k], 'description': known[k][1], 'valueType': known[k][0], 'consumer': known[k][2]} for k in known]
    sheet(WB, 'GlobalRule', cols, rows, 'GlobalRuleRecord', MAIN, '6 个未读取键已退役：圈数→Movement/LapRule，倒计时→Match/ModeRule，执念→Skill/Obsession')

    cols = [C('key', 'string#key', ''), C('value', 'string', ''), C('source', 'string', '', 'doc'), C('comment', 'string', '改动需同步代码，不由导出器写入', 'doc')]
    rows = [{'key': 'skill_slot_count', 'value': '3', 'source': 'SkillLoadout.cs:9', 'comment': 'RPC 签名写死 3 参数'}, {'key': 'max_ai', 'value': '5', 'source': 'RacerId.cs:10', 'comment': '数组容量 6，人格数校验'},
            {'key': 'ai_id_base', 'value': '10000', 'source': 'RacerId.cs:9', 'comment': '协议常量'}, {'key': 'tick_rate', 'value': '60', 'source': 'MainMenu.unity TimeManager', 'comment': 'fixedDeltaTime 必须等于 1/tick'},
            {'key': 'prediction_protocol_version', 'value': '1', 'source': 'PredictionProtocol.cs:12', 'comment': ''}, {'key': 'max_players', 'value': '6', 'source': '9 处重复', 'comment': '见筛查清单'}]
    sheet(WB, 'StructuralConstant', [C(c[0], c[1], c[3], 'doc') for c in cols], rows, '(只读) 结构性常量', '代码常量', '全列 doc：仅记录，不导出')

    dyn = X.first_doc('ProjectSettings/DynamicsManager.asset', 'PhysicsManager')
    cols = [C('profileId', 'string#key', ''), C('gravity', 'vector3', ''), C('bounceThreshold', 'float', ''), C('sleepThreshold', 'float', '', 'adv'), C('defaultContactOffset', 'float', '', 'adv'),
            C('defaultSolverIterations', 'int', '', 'adv'), C('defaultMaxAngularSpeed', 'float', '等于角色最大角速度'), C('fixedTimestep', 'float', '必须等于 1/tick_rate', 'adv')]
    sheet(WB, 'GlobalPhysics', cols, [{'profileId': 'default', 'gravity': vec(dyn['m_Gravity']), 'bounceThreshold': dyn['m_BounceThreshold'], 'sleepThreshold': dyn['m_SleepThreshold'], 'defaultContactOffset': dyn['m_DefaultContactOffset'],
                                       'defaultSolverIterations': dyn['m_DefaultSolverIterations'], 'defaultMaxAngularSpeed': dyn['m_DefaultMaxAngularSpeed'], 'fixedTimestep': 1/60}],
          '(新增, 只读候选) GlobalPhysicsRecord', 'ProjectSettings/DynamicsManager.asset')

    cols = [C('materialId', 'string#key', ''), C('dynamicFriction', 'float', ''), C('staticFriction', 'float', ''), C('bounciness', 'float', ''), C('frictionCombine', 'enum:PhysicMaterialCombine', ''), C('bounceCombine', 'enum:PhysicMaterialCombine', ''), C('assetPath', 'string#path', '', 'doc')]
    comb = {0: 'Average', 1: 'Multiply', 2: 'Minimum', 3: 'Maximum'}
    rows = []
    for p in sorted(__import__('glob').glob(os.path.join(ROOT, 'Assets/Material/*.physicMaterial'))):
        rel = os.path.relpath(p, ROOT).replace(os.sep, '/')
        m = X.first_doc(rel, 'PhysicMaterial')
        rows.append({'materialId': m['m_Name'], 'dynamicFriction': m['dynamicFriction'], 'staticFriction': m['staticFriction'], 'bounciness': m['bounciness'], 'frictionCombine': comb[m['frictionCombine']], 'bounceCombine': comb[m['bounceCombine']], 'assetPath': rel})
    sheet(WB, 'PhysicsMaterial', cols, rows, '(新增) PhysicsMaterialRecord', 'Assets/Material/*.physicMaterial')

    tm = X.first_doc('ProjectSettings/TagManager.asset', 'TagManager')
    rows = [{'name': t, 'usedBy': {'StartingPoint': 'LapProgress.startPointTag', 'TrackEdge': 'BlackCurtainViewController.trackEdgeTag', 'RespawnFloor': 'BuddahRespawn.respawnFloorTag', 'ScreenCamera': 'SharedAssets'}.get(t, '')} for t in tm['tags']]
    sheet(WB, 'Tag', [C('name', 'string#key', ''), C('usedBy', 'string', '', 'doc')], rows, '(只读) Tag', 'ProjectSettings/TagManager.asset')
    rows = [{'index': i, 'name': n, 'usedBy': {'Buddah': 'Buddah.prefab 根', 'BlackCurtainTrail': '拖尾子物体', 'TrackEdge': 'Events.cs:750 GetMask', 'RollbackBoundingBox': 'Events.cs:750 GetMask'}.get(n, '')} for i, n in enumerate(tm['layers']) if n]
    sheet(WB, 'Layer', [C('index', 'int#key', ''), C('name', 'string', ''), C('usedBy', 'string', '', 'doc')], rows, '(只读) Layer', 'ProjectSettings/TagManager.asset')

    ebs = X.first_doc('ProjectSettings/EditorBuildSettings.asset', 'EditorBuildSettings')
    rows = [{'buildIndex': i, 'sceneName': os.path.splitext(os.path.basename(s['path']))[0], 'path': s['path'], 'enabled': boolv(s['enabled'])} for i, s in enumerate(ebs['m_Scenes'])]
    sheet(WB, 'Scene', [C('buildIndex', 'int#key', ''), C('sceneName', 'string#key', '代码 SceneNames 常量'), C('path', 'string#path', ''), C('enabled', 'bool', '')], rows, '(只读) Scene', 'ProjectSettings/EditorBuildSettings.asset', 'RaceMapEndField 不存在但两处仍引用')

    ia = X.json_file('Assets/Scripts/InputSystem/InputSystem_Actions.inputactions')
    rows = []
    for mp in ia['maps']:
        for b in mp['bindings']:
            if b.get('isComposite'):
                continue
            rows.append({'map': mp['name'], 'action': b['action'], 'bindingPath': b['path'], 'scheme': b.get('groups', ''), 'codeOverride': {('Player', 'Movement'): 'PlayerBuddahInputSource.cs:45-48 硬编码 A/D',
                         ('Player', 'HandRotation'): 'BuddahHandControl.cs:412-418', ('Player', 'HandPush'): 'BuddahHandControl.cs:443,452; ComboSkillInput.cs:121-126'}.get((mp['name'], b['action']), '')})
    sheet(WB, 'InputBinding', [C('map', 'string#key', ''), C('action', 'string#key', ''), C('bindingPath', 'string#key', ''), C('scheme', 'string', ''), C('codeOverride', 'string', '代码绕过绑定的位置', 'doc')],
          rows, '(新增) InputBindingRecord', 'Assets/Scripts/InputSystem/InputSystem_Actions.inputactions', '改键需先去掉硬编码 KeyCode')

    rows = []
    for p in ['Assets/Animation/CG/TransitionToRaceMap.playable', 'Assets/Animation/CG/EndResultSwitchTimeline.playable', 'Assets/Animation/CG/LocalPlayerFinishTimeline.playable', 'Assets/Animation/CG/PropertySelectionEnd.playable']:
        sig = X.timeline_signals(p)
        rows.append({'timelineId': os.path.splitext(os.path.basename(p))[0], 'path': p, 'durationSeconds': X.playable_duration(p), 'signals': '|'.join(f'{fmt(t)}:{os.path.splitext(os.path.basename(a))[0]}' for t, a in sig),
                     'gameplayEffect': {'TransitionToRaceMap': '决定 GO 时刻 Max(timeline, 样条÷速度)', 'PropertySelectionEnd': '服务器等待后加载比赛场景', 'EndResultSwitchTimeline': '结算流程信号'}.get(os.path.splitext(os.path.basename(p))[0], '')})
    sheet(WB, 'Timeline', [C('timelineId', 'string#key', ''), C('path', 'string#path', ''), C('durationSeconds', 'float', '最长 clip 结束时间'), C('signals', 'list,string#sep=|', 'time:signalAsset'), C('gameplayEffect', 'string', '', 'doc')],
          rows, '(只读) 由 timeline 资产导出', 'Assets/Animation/CG/*.playable', '只读显示；改时长请改 timeline')

    enums = {
        'SkillSelectionPoolType': [('Selectable', 0), ('Fallback', 1), ('Default', 2), ('Hidden', 3), ('TestOnly', 4)],
        'SkillUnlockState': [('unlocked', 0), ('locked', 1), ('hidden', 2)],
        'SkillBehaviorType': [(n, i) for i, n in enumerate(['Acceleration', 'SlowTrap', 'BlackCurtain', 'Giant', 'PushProjectileHands', 'ReverseTurn', 'AccelerationAnti', 'SlowTrapAnti', 'BlackCurtainAnti', 'GiantAnti', 'PushProjectileHandsAnti', 'ReverseTurnAnti'])],
        'ComboToken': [('W', 0), ('Up', 1)], 'SkillTargetScope': [('Self', 0), ('AllOthers', 1)], 'StackPolicy': [('RefreshMaxDuration', 0), ('RefreshMaxDurationOverwriteMagnitude', 1), ('MaxMagnitudeMaxDuration', 2)],
        'ImpulseSourceType': [('MeleePush', 0), ('Projectile', 1), ('ChargedProjectile', 2)], 'ModifierCommandType': [(n, i) for i, n in enumerate(['Acceleration', 'RootThenAcceleration', 'InvertTurn', 'Scale', 'PushGrace', 'SteeringSuppression', 'RoomBypass'])],
        'TeleportSourceType': [('DropRespawn', 0), ('WrongWayCorrection', 1), ('Manual', 2)], 'SoloDifficulty': [('Easy', 0), ('Normal', 1), ('Hard', 2)], 'RolloutTailMode': [('Anchor', 0), ('KeepCandidate', 1), ('FullThrust', 2)],
        'AISkillTargetPreference': [('Ahead', 0), ('Behind', 1), ('PackCentre', 2), ('EnteringCorner', 3), ('Impaired', 4)], 'AISkillSituation': [('Pursued', 0), ('LeadingAlone', 1), ('Chasing', 2), ('Pack', 3), ('TrailingAlone', 4)],
        'MatchMode': [('Online', 0), ('Solo', 1), ('Practice', 2)], 'ReturnTarget': [('Room', 0), ('MainMenuHome', 1)], 'ResultPlayerChoice': [('None', 0), ('StartNewGame', 1), ('ReturnToRoom', 2)],
        'PropertySelectionMode': [('Single', 0), ('Multi', 1), ('Vote', 2), ('HostOnly', 3)], 'SelectionLayout': [('Carousel', 0), ('SkillWall', 1)], 'LobbyVisibility': [('Public', 0), ('FriendsOnly', 1), ('Private', 2)],
        'PhysicMaterialCombine': [('Average', 0), ('Multiply', 1), ('Minimum', 2), ('Maximum', 3)]}
    rows = [{'enumName': e, 'name': n, 'value': v, 'alias': '', 'comment': ''} for e, items in enums.items() for n, v in items]
    sheet(WB, '__enums__', [C('enumName', 'string#key', ''), C('name', 'string#key', ''), C('value', 'int', ''), C('alias', 'string', ''), C('comment', 'string', '', 'doc')], rows, '枚举定义（不导出，校验用）', '代码枚举 + 本规范新增枚举')

if __name__ == '__main__':
    build_skill(); build_movement(); build_ai(); build_match(); build_ui(); build_global()
    V2.migrate_all(MODEL)   # v1 builders above describe the project; the v2 layout is the committed shape
    for name, sheets in TM.write_model(MODEL, OUT):
        print('wrote', name, sheets)
    print('texts', len(text_rows))
