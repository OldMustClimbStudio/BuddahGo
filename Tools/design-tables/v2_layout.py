"""v2 layout of the design tables (Docs/design-tables/structure-audit.md §3, decided 2026-10-13).

Each migrate_* function rewrites one domain of a v1 Model in place. build_tables.py applies all four after
generating the v1 baseline; migrate_v2.py applies one phase to the committed workbooks. Every value is carried
over from the v1 sheets; nothing is re-read from the project here.
"""
from tablemodel import Sheet, fmt

ID_RENAME = {'anti_blackcurtain': 'blackcurtain_anti'}
SKILL_ORDER = ['acceleration', 'slowtrap', 'blackcurtain', 'giant', 'push_projectile_hands', 'reverseturn',
               'acceleration_anti', 'slowtrap_anti', 'blackcurtain_anti', 'giant_anti', 'push_projectile_hands_anti', 'reverseturn_anti']
# Skill.effectDurationSeconds is written to this SO field per skill (SkillDatabase.ApplyDesignTables mirrors it).
EFFECT_DURATION_FIELD = {
    'acceleration': 'durationSeconds', 'acceleration_anti': 'durationSeconds',
    'slowtrap': 'zoneDurationSeconds', 'slowtrap_anti': 'rootDurationSeconds',
    'blackcurtain': 'holdDurationSeconds', 'blackcurtain_anti': 'holdDurationSeconds',
    'giant': 'durationSeconds', 'giant_anti': 'durationSeconds',
    'push_projectile_hands': 'buffDurationSeconds',
    'reverseturn': 'invertDurationSeconds', 'reverseturn_anti': 'invertDurationSeconds',
}
# SkillEffectParam rows that never had an SO field: they become SkillConstant (presentation-only) rows.
CODE_CONSTANTS = {('slowtrap', 'zoneRadius'), ('push_projectile_hands', 'flightEndSpeedFraction'), ('push_projectile_hands', 'progressCompletionFraction'),
                  ('push_projectile_hands', 'ignoreSolidWorld'), ('push_projectile_hands_anti', 'reverseDirection'), ('push_projectile_hands_anti', 'allowSelfHit'),
                  ('push_projectile_hands_anti', 'ignoreSolidWorld'), ('reverseturn', 'targetScope'), ('reverseturn_anti', 'targetScope'),
                  ('blackcurtain', 'casterSeesTrackEdge'), ('blackcurtain_anti', 'casterSeesTrackEdge'), ('giant', 'scalesMaxSpeed'),
                  ('giant_anti', 'massMultiplier'), ('giant_anti', 'forwardForceMultiplier'), ('acceleration', 'stackRule')}


def C(var, typ, com='', grp=''):
    return (var, typ, grp, com)


def rid(skill_id):
    return ID_RENAME.get(str(skill_id), str(skill_id))


def num(v):
    """Cell text -> int/float when numeric, else unchanged ('' stays '')."""
    if isinstance(v, (int, float)) and not isinstance(v, bool):
        return v
    s = str(v).strip()
    if s == '':
        return ''
    try:
        f = float(s)
    except ValueError:
        return s
    return int(f) if f == int(f) and '.' not in s else f


def camel(name):
    return name[0].lower() + name[1:] if name else name


def rename_values(model, old, new):
    for s in model.sheets():
        for r in s.rows:
            for k, v in list(r.items()):
                if isinstance(v, str) and old in v:
                    r[k] = v.replace(old, new)


def retarget_refs(model, old, new):
    """Rewrite '#ref=<old>' (with or without a 'Workbook/' prefix) to '#ref=<new>' in every ##type cell."""
    for s in model.sheets():
        for i, (v, t, g, c) in enumerate(s.cols):
            if old in t:
                s.cols[i] = (v, t.replace(old, new), g, c)


# =====================================================================================================
# Skill domain: A1–A6, E1 (combo / cast-confirm keys leave GlobalRule)
# =====================================================================================================
def migrate_skill(m):
    wb = 'Skill'
    sd, sb, ep, hp = m.get(wb, 'SkillDefinition'), m.get(wb, 'SkillBalance'), m.get(wb, 'SkillEffectParam'), m.get(wb, 'HandPush')
    P = {(rid(r['skillId']), str(r['paramKey'])): r for r in ep.rows}

    def p(sid, key):
        r = P.get((sid, key))
        return num(r['paramValue']) if r else ''

    # --- Skill: identity + timing + obsession + primary effect duration ---
    cols = [C('skillId', 'enum:SkillId#key', '技能唯一 id，snake_case；名单在 Global/__enums__ 的 SkillId'),
            C('displayNameKey', 'string#ref=UI/Text.key', '显示名文案 key'), C('descriptionKey', 'string#ref=UI/Text.key', '描述文案 key'),
            C('sortOrder', 'int', '选择界面排序'), C('unlockState', 'enum:SkillUnlockState', 'unlocked / locked / hidden'),
            C('antiSkillId', 'string#ref=Skill.skillId', '反噬变体 id；普通技能必填，反噬行留空'),
            C('cooldownSeconds', 'float#range=0,120', '冷却，从请求时刻+前摇起算'), C('castLockSeconds', 'float#range=0,120', '施法锁定'),
            C('castConfirmDelaySeconds', 'float#range=0,5', '施法前摇；反噬行按本行生效（原 GlobalRule anti_cast_confirm_delay_seconds）'),
            C('obsessionGain', 'float#range=0,1000', '施法增加的执念；反噬按原技能取值，反噬行必须留空'),
            C('effectDurationSeconds', 'float#range=0,120', '主效果持续时间，写入技能 SO 的主时长字段：acceleration/giant→durationSeconds，slowtrap→zoneDurationSeconds，'
                                                             'slowtrap_anti→rootDurationSeconds，blackcurtain→holdDurationSeconds，push_projectile_hands→buffDurationSeconds，'
                                                             'reverseturn→invertDurationSeconds；push_projectile_hands_anti 瞬发无时长')]
    rows = []
    for d in sd.rows:
        sid = rid(d['skillId'])
        b = sb.row(skillId=d['skillId']) or {}
        anti = sid.endswith('_anti')
        rows.append({'skillId': sid, 'displayNameKey': d['displayNameKey'].replace('anti_blackcurtain', 'blackcurtain_anti'),
                     'descriptionKey': d['descriptionKey'].replace('anti_blackcurtain', 'blackcurtain_anti'),
                     'sortOrder': d['sortOrder'], 'unlockState': d['unlockState'], 'antiSkillId': rid(b.get('antiSkillId', '')) if b.get('antiSkillId') else '',
                     'cooldownSeconds': b.get('cooldownSeconds', ''), 'castLockSeconds': b.get('castLockSeconds', ''),
                     'castConfirmDelaySeconds': b.get('castConfirmDelaySeconds', ''), 'obsessionGain': '' if anti else b.get('obsessionGain', ''),
                     'effectDurationSeconds': p(sid, EFFECT_DURATION_FIELD[sid]) if sid in EFFECT_DURATION_FIELD else ''})
    rows.sort(key=lambda r: SKILL_ORDER.index(r['skillId']))
    skill = Sheet(wb, 'Skill', cols, rows, '(生成) SkillRecord ← SkillDefinition + SkillBalance + SkillEffectParam 主时长',
                  'Assets/Data/Config/ProjectConfigDatabase_Main.asset; Assets/Skills_Assets/Skills/*.asset',
                  '2026-10-13 v2：身份、冷却/锁定/前摇、执念、主效果时长合一；behaviorType/iconKey/isSelectable/tags 四个死列删除')

    # --- SkillEffect<Family>: strongly typed per-family parameters, one row for normal and one for anti ---
    def family(name, members, spec, notes=''):
        cols = [C('skillId', 'enum:SkillId#key#ref=Skill.skillId', '普通与反噬各一行；空格 = 该行的技能类没有这个字段')] + [C(v, t, c) for v, t, c in spec]
        # code constants (e.g. giant_anti massMultiplier) are not SO fields: they go to SkillConstant, not here
        rows = [{'skillId': sid, **{v: ('' if (sid, v) in CODE_CONSTANTS else p(sid, v)) for v, t, c in spec}} for sid in members]
        return m.add(Sheet(wb, name, cols, rows, f'(生成) {name}Record', 'Assets/Skills_Assets/Skills/*.asset', notes))

    fam = [
        family('SkillEffectAcceleration', ['acceleration', 'acceleration_anti'],
               [('extraForwardForce', 'float', '普通：额外前推力 N'), ('extraMaxSpeed', 'float', '普通：额外限速 m/s'),
                ('slowForwardForce', 'float', '反噬：减速力 N'), ('slowMaxSpeed', 'float', '反噬：限速削减 m/s')],
               '主时长 durationSeconds 在 Skill.effectDurationSeconds'),
        family('SkillEffectSlowTrap', ['slowtrap', 'slowtrap_anti'],
               [('perTargetReapplyCooldown', 'float', '普通：同一目标重复施加间隔 s'), ('affectCaster', 'bool', '普通：是否影响施法者'),
                ('slowForwardForce', 'float', '普通：减速力 N'), ('slowMaxSpeed', 'float', '普通：限速削减 m/s'), ('slowDurationSeconds', 'float', '普通：每次减速持续 s'),
                ('extraForwardForce', 'float', '反噬：定身后加速力 N'), ('extraMaxSpeed', 'float', '反噬：定身后额外限速 m/s'), ('accelerationDurationSeconds', 'float', '反噬：定身后加速持续 s')],
               '区域时长 zoneDurationSeconds / 定身时长 rootDurationSeconds 在 Skill.effectDurationSeconds；区域半径见 SkillConstant'),
        family('SkillEffectBlackCurtain', ['blackcurtain', 'blackcurtain_anti'],
               [('expandDurationSeconds', 'float', '展开 s'), ('fadeOutDurationSeconds', 'float', '淡出 s'), ('maxOpacity', 'float#range=0,1', '最大不透明度')],
               '保持时长 holdDurationSeconds 在 Skill.effectDurationSeconds'),
        family('SkillEffectGiant', ['giant', 'giant_anti'],
               [('scaleMultiplier', 'float', '体型倍率（反噬 <1）'), ('shrinkDurationSeconds', 'float', '缩回 / 缩小过渡 s'), ('growDurationSeconds', 'float', '普通：变大过渡 s'),
                ('massMultiplier', 'float', '普通：质量倍率（反噬代码固定 1）'), ('forwardForceMultiplier', 'float', '普通：推力倍率（反噬代码固定 1）'),
                ('restoreDurationSeconds', 'float', '反噬：恢复过渡 s')],
               '持续 durationSeconds 在 Skill.effectDurationSeconds'),
    ]

    # --- Push: basic hand push + charged projectile + anti burst share one column set ---
    basic = hp.rows[0]
    ch, an = 'push_projectile_hands', 'push_projectile_hands_anti'
    push_cols = [C('pushId', 'string#key', 'basic=普通推掌（BuddahHandControl），charged=如来神掌蓄力弹（Skill_PushProjectileHands），charged_anti=反噬弹幕（Skill_PushProjectileHands_Anti）'),
                 C('rotationSpeedDegPerSec', 'float', 'basic：手部旋转速度（AI 也读）'),
                 C('windupSeconds', 'float', 'basic=pushWindupSeconds 前摇；charged / charged_anti=buildUpSeconds 蓄力'),
                 C('pushCooldownSeconds', 'float', 'basic=pushCooldownSeconds；charged=chargedPushCooldownSeconds'),
                 C('delayedPushActionSeconds', 'float', 'charged：蓄力后延迟推掌动作 s'),
                 C('lifetimeSeconds', 'float', 'basic=hitbox 存活 pushLifetimeSeconds；charged=projectileLifetimeSeconds'),
                 C('impulseStrength', 'float', 'basic=pushImpulseStrength；charged=projectileImpulseStrength'),
                 C('speed', 'float', 'charged：projectileSpeed m/s'),
                 C('forwardOffset', 'float', 'basic=pushForwardOffset；charged=projectileForwardOffset；charged_anti=additionalForwardSpawnOffset'),
                 C('heightOffset', 'float', 'basic=pushHeightOffset；charged=projectileHeightOffset；charged_anti=additionalHeightSpawnOffset'),
                 C('sideOffset', 'float', 'basic=pushSideOffset'), C('leftSideBias', 'float', 'basic=pushLeftSideBias'), C('rightSideBias', 'float', 'basic=pushRightSideBias'),
                 C('projectileCount', 'int', 'charged_anti：弹幕数量'), C('projectileSpacing', 'float', 'charged_anti：弹幕间距 m'),
                 C('colliderSize', 'vector3', 'charged：projectileColliderSize；basic 的 hitbox 尺寸见 SkillConstant'),
                 C('hitTurnTorqueImpulse', 'float', 'charged_anti：命中时的转向冲量')]
    push_rows = [
        {'pushId': 'basic', 'rotationSpeedDegPerSec': basic['rotationSpeedDegPerSec'], 'windupSeconds': basic['pushWindupSeconds'], 'pushCooldownSeconds': basic['pushCooldownSeconds'],
         'lifetimeSeconds': basic['pushLifetimeSeconds'], 'impulseStrength': basic['pushImpulseStrength'], 'forwardOffset': basic['pushForwardOffset'],
         'heightOffset': basic['pushHeightOffset'], 'sideOffset': basic['pushSideOffset'], 'leftSideBias': basic['pushLeftSideBias'], 'rightSideBias': basic['pushRightSideBias']},
        {'pushId': 'charged', 'windupSeconds': p(ch, 'buildUpSeconds'), 'pushCooldownSeconds': p(ch, 'chargedPushCooldownSeconds'), 'delayedPushActionSeconds': p(ch, 'delayedPushActionSeconds'),
         'lifetimeSeconds': p(ch, 'projectileLifetimeSeconds'), 'impulseStrength': p(ch, 'projectileImpulseStrength'), 'speed': p(ch, 'projectileSpeed'),
         'forwardOffset': p(ch, 'projectileForwardOffset'), 'heightOffset': p(ch, 'projectileHeightOffset'), 'colliderSize': p(ch, 'projectileColliderSize')},
        {'pushId': 'charged_anti', 'windupSeconds': p(an, 'buildUpSeconds'), 'lifetimeSeconds': p(an, 'projectileLifetimeSeconds'), 'impulseStrength': p(an, 'projectileImpulseStrength'),
         'speed': p(an, 'projectileSpeed'), 'forwardOffset': p(an, 'additionalForwardSpawnOffset'), 'heightOffset': p(an, 'additionalHeightSpawnOffset'),
         'projectileCount': p(an, 'projectileCount'), 'projectileSpacing': p(an, 'projectileSpacing'), 'colliderSize': p(an, 'projectileColliderSize'),
         'hitTurnTorqueImpulse': p(an, 'hitTurnTorqueImpulse')}]
    push = Sheet(wb, 'Push', push_cols, push_rows, '(生成) PushRecord ← BuddahHandControl + Skill_PushProjectileHands(_Anti)',
                 'Assets/Character/Prefab/Buddah.prefab; Assets/Skills_Assets/Skills/Skill_PushProjectileHands*.asset',
                 'basic 行覆盖 BuddahHandControl，charged / charged_anti 行在 Play 模式覆盖两个技能 SO（SkillDatabase.ApplyDesignTables 的显式映射）')

    # --- SkillConstant: effect numbers that live in code or prefabs (recorded, never exported) ---
    const_cols = [C('ownerId', 'string#key', '技能 id，或 push_basic'), C('paramKey', 'string#key', ''), C('value', 'string', ''), C('source', 'string', '代码 / 资源位置', 'doc')]
    const_rows = [{'ownerId': 'push_basic', 'paramKey': 'hitboxSize', 'value': basic['hitboxSize'], 'source': 'Cube.prefab BoxCollider size × scale'},
                  {'ownerId': 'push_basic', 'paramKey': 'hitboxCenter', 'value': basic['hitboxCenter'], 'source': 'Cube.prefab BoxCollider center'},
                  {'ownerId': 'push_basic', 'paramKey': 'leftHandTriggerName', 'value': basic['leftHandTriggerName'], 'source': 'Animator trigger（BuddahHandControl）'},
                  {'ownerId': 'push_basic', 'paramKey': 'rightHandTriggerName', 'value': basic['rightHandTriggerName'], 'source': 'Animator trigger（BuddahHandControl）'}]
    for r in ep.rows:
        sid = rid(r['skillId'])
        if (sid, str(r['paramKey'])) in CODE_CONSTANTS:
            const_rows.append({'ownerId': sid, 'paramKey': r['paramKey'], 'value': r['paramValue'], 'source': r.get('unit', '')})
    const = Sheet(wb, 'SkillConstant', const_cols, const_rows, '(只读) 代码与 prefab 里的效果常量', '代码常量；Cube.prefab；SlowTrapZone.prefab', '改动需同步代码或资源，不由导出器写入')

    # --- ComboInput: the two combo keys leave GlobalRule ---
    gr = m.get('Global', 'GlobalRule')
    combo = Sheet(wb, 'ComboInput', [C('profileId', 'string#key', ''), C('inputWindowSeconds', 'float#range=0.05,2', '连续按键最大间隔，超时清空缓冲（ComboSkillInput.stepWindowSeconds）'),
                                     C('maxBuffer', 'int#range=1,16', '搓招输入缓冲长度（ComboSkillInput.maxBuffer）')],
                  [{'profileId': 'default', 'inputWindowSeconds': num(gr.value('ruleValue', ruleKey='combo_input_window_seconds')), 'maxBuffer': num(gr.value('ruleValue', ruleKey='combo_max_buffer'))}],
                  '(生成) ComboInputRecord ← GlobalRule combo_*', 'Assets/Data/Config/ProjectConfigDatabase_Main.asset; Buddah.prefab ComboSkillInput', '原 GlobalRule 的 combo_input_window_seconds / combo_max_buffer')
    retired = {'combo_input_window_seconds', 'combo_max_buffer', 'cast_confirm_delay_seconds', 'anti_cast_confirm_delay_seconds'}
    gr.rows = [r for r in gr.rows if r['ruleKey'] not in retired]
    gr.notes = '只放真正跨域的开关；搓招键→Skill/ComboInput，施法前摇→Skill.castConfirmDelaySeconds（含反噬行），圈数→Match/Track，倒计时→Match/ModeRule，执念→Skill/Obsession'

    # --- enums: SkillId registry replaces the unused SkillBehaviorType ---
    en = m.get('Global', ENUMS)
    en.rows = [r for r in en.rows if r['enumName'] != 'SkillBehaviorType']
    en.rows += [{'enumName': 'SkillId', 'name': sid, 'value': i, 'alias': '', 'comment': '唯一技能名单；SO skillId、AI 配装、图标表都以此为准'} for i, sid in enumerate(SKILL_ORDER)]

    # --- text: renamed keys and the 逆姻缘 spelling (decided 2026-10-13) ---
    tx = m.get('UI', 'Text')
    for r in tx.rows:
        for k in ('zh', 'en'):
            if isinstance(r.get(k), str):
                r[k] = r[k].replace('逆因缘', '逆姻缘').replace('法相金身 （噬）', '法相金身（噬）')
        if r.get('context') in ('SkillDefinition.displayName', 'SkillDefinition.description'):
            r['context'] = 'Skill.' + ('displayNameKey' if r['context'].endswith('displayName') else 'descriptionKey')

    # --- workbook assembly ---
    old = m.workbooks[wb]
    keep = {k: v for k, v in old.items() if k not in ('SkillDefinition', 'SkillBalance', 'SkillEffectParam', 'HandPush',
                                                         'SkillEffectAcceleration', 'SkillEffectSlowTrap', 'SkillEffectBlackCurtain', 'SkillEffectGiant')}
    ordered = {}
    ordered['Skill'] = skill
    for f in fam:
        ordered[f.name] = f
    ordered['Push'] = push
    for name in ('SkillPresentation', 'VfxRegistry', 'FeelEvent', 'SkillSelectionRule', 'DefaultLoadout'):
        ordered[name] = keep.pop(name)
    ordered['ComboInput'] = combo
    for name in ('ComboBinding', 'Obsession', 'BlackCurtainPresentation'):
        ordered[name] = keep.pop(name)
    ordered.update(keep)
    ordered['SkillConstant'] = const
    m.workbooks[wb] = ordered

    retarget_refs(m, 'SkillDefinition.skillId', 'Skill.skillId')
    rename_values(m, 'anti_blackcurtain', 'blackcurtain_anti')
    m.get(wb, 'SkillPresentation').set_col('vfxDurationSeconds', com='0=跟随 Skill.effectDurationSeconds；slowtrap 的 11 = 区域 10 s + 1 s 余韵')


ENUMS = '__enums__'


# =====================================================================================================
# Movement domain: B1–B5 (LapRule / WrongWayRule move into Match)
# =====================================================================================================
def migrate_movement(m):
    wb = 'Movement'
    mb, pr, lh, rr = m.get(wb, 'MovementBase'), m.get(wb, 'PushReaction'), m.get(wb, 'LaunchHandoff'), m.get(wb, 'RespawnRule')
    lap, ww = m.get(wb, 'LapRule'), m.get(wb, 'WrongWayRule')
    b, p, h = mb.rows[0], pr.row(impulseSource='MeleePush'), lh.rows[0]

    cols = [c for c in mb.cols if c[2] != 'doc'] + [
        C('pushGraceSeconds', 'float', '受击宽限 s（原 PushReaction.graceSeconds，三种来源数值相同）'),
        C('pushExtraMaxSpeed', 'float', '受击宽限期临时限速增量（原 PushReaction.extraMaxSpeed）'),
        C('launchInheritSeconds', 'float', '起跑交接：继承开场速度的时长（BuddahMovement.launchInheritTime）'),
        C('launchBlendSeconds', 'float', '起跑交接：线性混合时长（BuddahMovement.launchBlendTime）'),
        C('handoffGroundSnapMaxDistance', 'float', '交接时地面吸附最大距离'), C('handoffGroundProbeHeight', 'float', '交接时地面探测高度'),
        C('velocitySampleDeltaSeconds', 'float', 'RaceBodyIntroStateController 速度采样间隔', 'adv')] + [c for c in mb.cols if c[2] == 'doc'] + [
        C('launchSuppressTurnSeconds', 'float', '代码常量 RaceBodyIntroStateController.cs:560，未接表', 'doc'),
        C('minPreHandoffBypassSeconds', 'float', '代码常量 Events.cs:173 下限，未接表', 'doc'),
        C('clearAngularVelocity', 'bool', '代码写死 false，未接表', 'doc')]
    row = dict(b)
    row.update({'pushGraceSeconds': p['graceSeconds'], 'pushExtraMaxSpeed': p['extraMaxSpeed'], 'launchInheritSeconds': h['inheritSeconds'], 'launchBlendSeconds': h['blendSeconds'],
                'handoffGroundSnapMaxDistance': h['groundSnapMaxDistance'], 'handoffGroundProbeHeight': h['groundProbeHeight'], 'velocitySampleDeltaSeconds': h['velocitySampleDeltaSeconds'],
                'launchSuppressTurnSeconds': h['suppressTurnSeconds'], 'minPreHandoffBypassSeconds': h['minPreHandoffBypassSeconds'], 'clearAngularVelocity': h['clearAngularVelocity']})
    mb.set_col('profileId', com='目前只有 default（玩家与 AI 共用）')
    movement = Sheet(wb, 'Movement', cols, [row], '(生成) MovementRecord ← MovementBase + PushReaction + LaunchHandoff',
                     'Assets/Character/Prefab/Buddah.prefab BuddahPredictedMotorConfig / Rigidbody / BuddahMovement / RaceBodyIntroStateController',
                     '2026-10-13 v2：同一组件的三张表合一；handoffLeadSeconds 只留 Match/Intro；impulseMultiplier（代码无区分）删除')

    rows = [dict(rr.rows[0])]
    rows[0].pop('teleportSource', None)
    rows[0]['profileId'] = 'default'
    rcols = [C('profileId', 'string#key', '三种传送来源（DropRespawn / WrongWayCorrection / Manual）共用一套数值')] + \
            [c for c in rr.cols if c[0] not in ('teleportSource',) and c[1] != 'bool'] + \
            [C(v, t, 'RespawnBridge 写死，未接表', 'doc') for v, t, g, c in rr.cols if t == 'bool']
    respawn = Sheet(wb, 'Respawn', rcols, rows, '(生成) RespawnRecord ← BuddahRespawn', 'Assets/Character/Prefab/Buddah.prefab', 'v2：按来源分行的三份相同数值合为一行；flags 列转 doc')

    # Track absorbs LapRule; TrackWrongWay is a Track sub-sheet
    tr = m.get('Match', 'Track')
    lap_row = lap.rows[0]
    lap_cols = [c for c in lap.cols if c[0] not in ('trackId', 'lapsToFinish')]
    for i, c in enumerate(lap_cols):
        tr.insert_col('lapsToFinish' if i == 0 else lap_cols[i - 1][0], c[0], c[1], c[3] or {'crossCooldownSeconds': 'LapProgress.minimumCrossingCooldownSeconds', 'armDistanceMeters': 'LapProgress.minimumWorldDistanceFromStartToArmLap',
                                                                                             'requiredCheckpointCount': '须等于 TrackCheckpoint 行数'}.get(c[0], ''), c[2])
        tr.rows[0][c[0]] = lap_row[c[0]]
    tr.set_col('lapsToFinish', com='完赛圈数（RaceFinishManager 与 LapProgress 共用，唯一来源）')
    tr.notes = 'v2：并入原 Movement/LapRule；圈数只留此处'
    tww = Sheet('Match', 'TrackWrongWay', [C('trackId', 'string#key#ref=Track.trackId', '')] + [C(v, t, c or v, g) for v, t, g, c in ww.cols if v != 'trackId'],
                [dict(ww.rows[0])], '(生成) TrackWrongWayRecord ← RaceCompletionTracker', 'Assets/Character/Prefab/Buddah.prefab', 'v2：原 Movement/WrongWayRule')
    m.insert_after('Match', 'Track', tww)
    m.get('Match', 'Intro').set_col('handoffLeadSeconds', com='起跑交接提前量（唯一来源；IntroSequenceManager 与 RaceBodyIntroStateController.defaultHandoffLeadTime 共用）')

    old = m.workbooks[wb]
    ordered = {'Movement': movement, 'Respawn': respawn}
    for k, v in old.items():
        if k not in ('MovementBase', 'PushReaction', 'LaunchHandoff', 'RespawnRule', 'LapRule', 'WrongWayRule', 'Movement', 'Respawn'):
            ordered[k] = v
    m.workbooks[wb] = ordered
    mr = m.get(wb, 'ModifierRule')
    mr.notes = 'presentationOnly：叠加策略硬编码在 Events/Resolver，仅记录'


# =====================================================================================================
# AI domain: D1–D4
# =====================================================================================================
def migrate_ai(m):
    wb = 'AI'
    dt = m.get(wb, 'DrivingTier')
    for v in list(dt.vars):
        if v != 'difficulty':
            dt.rename_col(v, camel(v))
    dt.record = 'AIDifficultyProfile（表头 camelCase，覆盖时按名忽略大小写）'
    dt.notes = 'adv 列为规划器高级参数；替代 Tools/ai/difficulty-*.json 生成链'

    dg = m.get(wb, 'DrivingGlobal')
    tuned = ['targetSpeedMin', 'targetSpeedMax', 'recoverySpeed', 'recoveryVelocityErrorDegrees']
    tcols = [C('profileId', 'string#key', '')] + [C(k, 'float', dg.value('comment', key=k)) for k in tuned]
    tuning = Sheet(wb, 'DrivingTuning', tcols, [{'profileId': 'default', **{k: dg.value('value', key=k) for k in tuned}}],
                   '(生成) DrivingTuningRecord', 'AIDifficultyProfiles.Resolve; ThrustVectorPlanner.cs', 'v2：原 DrivingGlobal 中有读取方的 4 个键')
    const = Sheet(wb, 'DrivingConstant', [C('key', 'string#key', ''), C('value', 'float', ''), C('source', 'string', '', 'doc'), C('comment', 'string', '', 'doc')],
                  [dict(r) for r in dg.rows if r['key'] not in tuned], '(只读) 规划器内部常量', 'ThrustVectorPlanner.cs 等', 'presentationOnly：[Eng] 项为代码常量，改动需同步代码')

    sd = m.get(wb, 'SkillDifficulty')
    sd.drop_col('Difficulty')
    for v in list(sd.vars):
        if v != 'difficulty':
            sd.rename_col(v, camel(v))
    if sd.has('impairedDrivingReactionTicks'):
        # a rebuilt baseline already reads the split field from SkillPersonalities.json
        sd.set_col('impairedDrivingReactionTicks', com='视野受损时驾驶反应额外 tick（AIRacerDriver）')
    else:
        sd.insert_col('impairedReactionTicks', 'impairedDrivingReactionTicks', 'int', '视野受损时驾驶反应额外 tick（AIRacerDriver）')
        for r in sd.rows:
            r['impairedDrivingReactionTicks'] = r['impairedReactionTicks']
    sd.set_col('impairedReactionTicks', com='视野受损时技能施放反应额外 tick（AISkillCaster）')
    sd.record = 'AISkillDifficulty（表头 camelCase，覆盖时按名忽略大小写）'
    sd.notes = 'v2：ImpairedReactionTicks 一列两用拆为技能 / 驾驶两列'

    pe, pw = m.get(wb, 'Personality'), m.get(wb, 'PersonalitySkillWeight')
    for i in range(3):
        pe.insert_col('slot2SkillId' if i == 0 else f'slot{i - 1}Weight', f'slot{i}Weight', 'float#range=0,1.5', f'第 {i + 1} 槽技能的选择权重')
    for r in pe.rows:
        for i in range(3):
            r[f'slot{i}Weight'] = pw.value('weight', personalityId=r['personalityId'], skillId=r[f'slot{i}SkillId'])
    pe.notes = 'v2：权重并入配装三槽，原 PersonalitySkillWeight 的 15 行死权重删除；人格数必须等于 Roster 行数（当前 5）'

    sg = m.get(wb, 'SkillGlobal')
    doc_keys = {'scaleShrunkThreshold', 'trackTableCellMeters'}
    cols = [C('profileId', 'string#key', '')]
    row = {'profileId': 'default'}
    for r in sg.rows:
        key = camel(str(r['key']))
        rng = str(r.get('range', '') or '')
        typ = 'int' if key in ('packMinNeighbours', 'maxComboRetries') else 'float'
        parts = rng.split('-') if rng else []
        if len(parts) == 2 and all(_isnum(x) for x in parts):
            typ += f'#range={parts[0]},{parts[1]}'
        com = (f'校验范围 {rng}；' if rng and '#range' not in typ else '') + str(r.get('source', '') or '')
        cols.append(C(key, typ, com + ('；代码常量，未接 AISkillTuning' if key in doc_keys else ''), 'doc' if key in doc_keys else ''))
        row[key] = r['value']
    cols = [c for c in cols if c[2] != 'doc'] + [c for c in cols if c[2] == 'doc']
    st = Sheet(wb, 'SkillTuning', cols, [row], '(生成) SkillTuningRecord → AISkillTuning（按名忽略大小写）', 'SkillPersonalities.json Tuning; AISkillWorld/Decision/Caster/Commitment.cs',
               'v2：原 SkillGlobal 键值桶改为单行宽表')

    old = m.workbooks[wb]
    ordered = {}
    for k, v in old.items():
        if k == 'DrivingTier':
            ordered[k] = v; ordered['DrivingTuning'] = tuning
        elif k == 'DrivingGlobal':
            continue
        elif k == 'PersonalitySkillWeight':
            continue
        elif k == 'SkillGlobal':
            ordered['SkillTuning'] = st
        else:
            ordered[k] = v
    ordered['DrivingConstant'] = const
    m.workbooks[wb] = ordered


def _isnum(s):
    try:
        float(s)
        return True
    except ValueError:
        return False


# =====================================================================================================
# Match domain: C2, C4
# =====================================================================================================
def migrate_match(m):
    wb = 'Match'
    mr, rn = m.get(wb, 'ModeRule'), m.get(wb, 'RacerNaming')
    default_diff = rn.value('value', key='defaultDifficulty') or 'Normal'
    mr.insert_col('aiCount', 'defaultDifficulty', 'enum:SoloDifficulty', '单人设置面板的默认难度（SoloSetupPanel；原 RacerNaming.defaultDifficulty）')
    for r in mr.rows:
        r['defaultDifficulty'] = default_diff if r['modeId'] in ('Solo', 'Practice') else ''
    mr.notes = 'Practice 行 = Solo 且 aiCount=0，目前 UI 无入口；v2 并入 RacerNaming.defaultDifficulty，命名前缀 key 直接在 UI/Text'
    m.remove(wb, 'RacerNaming')

    ss, so = m.get(wb, 'SelectionStage'), m.get(wb, 'SelectionOption')
    for r in ss.rows:
        if r['stageKey'] in ('map', 'skin'):
            r['_marker'] = '##'
    for r in so.rows:
        if r['stageKey'] in ('map', 'skin'):
            r['_marker'] = '##'
    ss.set_col('stageKey', com='map / skill_loadout / skin；map 与 skin 行以 ## 屏蔽：占位阶段，代码兜底，有真实地图/皮肤后取消屏蔽')
    ss.notes = 'v2：地图/皮肤占位行屏蔽（结果无人读取）'
    so.notes = 'v2：占位选项全部屏蔽，PropertiesSelectionManager 回退到代码占位'


PHASES = {'skill': migrate_skill, 'movement': migrate_movement, 'ai': migrate_ai, 'match': migrate_match}


def migrate_all(m):
    for fn in PHASES.values():
        fn(m)


# =====================================================================================================
# Editability pass (2026-10-13): every exported column has a reader, or is marked doc / presentationOnly
# =====================================================================================================
def _doc(sheet, var, com):
    sheet.set_col(var, grp='doc', com=com)


def _docs_last(sheet):
    sheet.cols = [c for c in sheet.cols if c[2] != 'doc'] + [c for c in sheet.cols if c[2] == 'doc']


def migrate_editability(m):
    mv = m.get('Movement', 'Movement')
    _doc(mv, 'autoThrottle', '油门恒 1，代码常量，只记录')
    _doc(mv, 'physicMaterial', '资源引用留在 prefab，只记录')
    mv.set_col('colliderRadius', com='角色实体胶囊（BuddahPredictedMotorConfig 在 Awake 写入 CapsuleCollider）')
    mv.set_col('colliderHeight', com='同上')
    mv.set_col('colliderCenter', com='同上')
    _docs_last(mv)

    mr = m.get('Match', 'ModeRule')
    _doc(mr, 'minPlayersToStart', '无读取方，只记录（开局门槛由 requiresReady 与准备状态决定）')
    _doc(mr, 'hostAutoReady', '无读取方，只记录')
    mr.set_col('requiresReady', com='RoomStateManager 每局开始时读取：是否所有客户端准备后主机才能开始')
    for v in ('allowSkipSpectating', 'allowPause', 'showRacerNameTags', 'showOpponentsOnMinimap'):
        mr.set_col(v, com='占位：经 IMatchRules 暴露，游戏内尚无读取方（S6/S7）')
    _docs_last(mr)

    tr = m.get('Match', 'Track')
    _doc(tr, 'minimapSpritePath', '资源引用留在 prefab，只记录')
    _doc(tr, 'minimapImageWidth', '只记录（MiniMapController 按 RectTransform 实际尺寸计算）')
    tr.set_col('trackSplineSamples', com='TrackSplineRef.samples 弧长表分辨率（场景值为兜底）')
    _docs_last(tr)

    lb = m.get('Match', 'Lobby')
    _doc(lb, 'minPlayers', '无读取方，只记录')
    _doc(lb, 'fishnetMaximumClients', 'ServerManager 场景设置，只记录')
    lb.set_col('defaultVisibility', com='SteamLobbyManager.PendingVisibility 初值')
    lb.set_col('maxPlayerNameLength', com='RoomStateManager 玩家名截断长度')
    _docs_last(lb)

    ss = m.get('Match', 'SelectionStage')
    for v in ('order', 'enabledOnline', 'enabledSolo', 'layoutMode', 'autoFillOnTimeout'):
        _doc(ss, v, '占位：PropertiesSelectionManager 未读取')
    _docs_last(ss)
    so = m.get('Match', 'SelectionOption')
    so.set_col('iconKey', com='SelectablePropertyOption.PreviewIconKey')
    so.set_col('unlocked', com='SelectablePropertyOption.IsUnlocked')

    tx = m.get('UI', 'Text')
    _doc(tx, 'en', '英文：本地化尚未接入，运行时只读 zh')
    _docs_last(tx)

    gr = m.get('Global', 'GlobalRule')
    _doc(gr, 'description', '说明，只记录')
    _docs_last(gr)

    for wb, name, note in (('Match', 'ResultVoteRule', 'presentationOnly：法定规则硬编码在 ResultDecisionManager.EvaluateDecisionServer，仅记录'),
                           ('Match', 'IntroLayout', 'presentationOnly：布局 prefab 引用留在场景，仅记录'),
                           ('UI', 'SkillIcon', 'presentationOnly：图标引用留在 SkillUiSpriteLibrary prefab，仅记录')):
        m.get(wb, name).notes = note


PHASES['editability'] = migrate_editability
