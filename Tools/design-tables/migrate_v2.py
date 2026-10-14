"""Apply one v2 layout phase to the committed workbooks in place (xlsx + CSV mirror + scope.json).

Usage: python Tools/design-tables/migrate_v2.py <repo_root> --phase skill|movement|ai|match [--out <dir>]
--out writes to another directory instead of Assets/Design/Tables (dry run / inspection).
Workbooks that a phase does not touch are left byte-identical so the importer's hash cache stays valid.
A workbook locked by Excel is written next to the original as <Name>.xlsx.pending; copy it over after closing Excel.
"""
import argparse, json, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tablemodel as TM
import v2_layout as V2

SCOPE_EDITS = {
    'skill': {
        'export': {'Skill': ['Skill', 'SkillEffectAcceleration', 'SkillEffectSlowTrap', 'SkillEffectBlackCurtain', 'SkillEffectGiant', 'Push',
                             'SkillSelectionRule', 'DefaultLoadout', 'ComboInput', 'ComboBinding', 'Obsession']},
        'presentationOnly': {'Skill': ['SkillPresentation', 'VfxRegistry', 'FeelEvent', 'BlackCurtainPresentation', 'SkillConstant']},
        'handWrittenRecords': ['SkillSelectionRuleRecord', 'DefaultLoadoutRecord', 'GlobalRuleRecord'],
        'tags_remove': ['Skill/SkillDefinition', 'Skill/SkillBalance', 'Skill/SkillEffectParam', 'Skill/HandPush'],
        'tags_add': {'Skill/Skill': ['skill', 'info', 'balance'], 'Skill/SkillEffectAcceleration': ['skill', 'balance'], 'Skill/SkillEffectSlowTrap': ['skill', 'balance'],
                     'Skill/SkillEffectBlackCurtain': ['skill', 'balance'], 'Skill/SkillEffectGiant': ['skill', 'balance'], 'Skill/Push': ['skill', 'balance'],
                     'Skill/ComboInput': ['skill', 'balance']},
    },
    'movement': {
        'export': {'Movement': ['Movement', 'Respawn'],
                   'Match': ['ModeRule', 'Track', 'TrackWrongWay', 'TrackSpawn', 'TrackCheckpoint', 'TrackResultAnchor', 'Lobby', 'ResultVoteRule', 'SelectionStage', 'SelectionOption', 'Intro', 'IntroLayout', 'RacerNaming']},
        'presentationOnly': {'Movement': ['ModifierRule', 'Camera', 'RacerVisual', 'SplineTracking']},
        'tags_remove': ['Movement/MovementBase', 'Movement/PushReaction', 'Movement/ModifierRule', 'Movement/LaunchHandoff', 'Movement/RespawnRule', 'Movement/LapRule', 'Movement/WrongWayRule'],
        'tags_add': {'Movement/Movement': ['movement', 'balance'], 'Movement/Respawn': ['movement', 'balance'], 'Match/TrackWrongWay': ['match', 'track', 'balance'],
                     'Match/Track': ['match', 'track', 'balance']},
    },
    'ai': {
        'export': {'AI': ['DrivingTier', 'DrivingTuning', 'SkillDifficulty', 'Personality', 'PersonalitySituationBias', 'SkillOpportunity', 'SkillTuning', 'Roster']},
        'presentationOnly': {'AI': ['DrivingConstant']},
        'tags_remove': ['AI/DrivingGlobal', 'AI/PersonalitySkillWeight', 'AI/SkillGlobal'],
        'tags_add': {'AI/DrivingTuning': ['ai', 'balance'], 'AI/SkillTuning': ['ai', 'balance']},
    },
    'match': {
        'export': {'Match': ['ModeRule', 'Track', 'TrackWrongWay', 'TrackSpawn', 'TrackCheckpoint', 'TrackResultAnchor', 'Lobby', 'ResultVoteRule', 'SelectionStage', 'SelectionOption', 'Intro', 'IntroLayout']},
        'tags_remove': ['Match/RacerNaming'],
        'tags_add': {},
    },
    'editability': {
        'export': {'Match': ['ModeRule', 'Track', 'TrackWrongWay', 'TrackSpawn', 'TrackCheckpoint', 'TrackResultAnchor', 'Lobby', 'SelectionStage', 'SelectionOption', 'Intro'],
                   'UI': ['Text', 'MenuOption']},
        'presentationOnly': {'Match': ['ResultVoteRule', 'ResultPresentation', 'IntroLayout', 'IntroSlotStyle'], 'UI': ['SkillIcon', 'HudObsession', 'UiTiming', 'MiniMap']},
        'tags_remove': ['Match/ResultVoteRule', 'Match/IntroLayout', 'UI/SkillIcon'],
        'tags_add': {},
    },
}


def edit_scope(path, phase):
    scope = json.load(open(path, encoding='utf-8'))
    e = SCOPE_EDITS[phase]
    for wb, sheets in e.get('export', {}).items():
        scope['export'][wb] = sheets
    for wb, sheets in e.get('presentationOnly', {}).items():
        scope['presentationOnly'][wb] = sheets
    if 'handWrittenRecords' in e:
        scope['handWrittenRecords'] = e['handWrittenRecords']
    for key in e.get('tags_remove', []):
        scope['tags'].pop(key, None)
    for key, tags in e.get('tags_add', {}).items():
        scope['tags'][key] = tags
    # keep tags ordered like the export lists
    ordered = {}
    for wb, sheets in scope['export'].items():
        for s in sheets:
            k = f'{wb}/{s}'
            if k in scope['tags']:
                ordered[k] = scope['tags'][k]
    scope['tags'] = ordered
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(scope, f, indent=2, ensure_ascii=False)
        f.write('\n')


def snapshot(model):
    out = {}
    for wb, sheets in model.workbooks.items():
        out[wb] = repr([(s.name, s.cols, s.rows, s.record, s.source, s.notes) for s in sheets.values()])
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('root')
    ap.add_argument('--phase', required=True, choices=list(V2.PHASES))
    ap.add_argument('--out')
    args = ap.parse_args()
    tables = os.path.join(args.root, 'Assets', 'Design', 'Tables')
    out = args.out or tables
    model = TM.read_tables(tables)
    before = snapshot(model)
    V2.PHASES[args.phase](model)
    model.rebuild_meta()
    after = snapshot(model)
    changed = [wb for wb in model.workbooks if before.get(wb) != after.get(wb)]
    if args.out:
        changed = list(model.workbooks)
    pending = []
    for wb in changed:
        try:
            TM.write_workbook_xlsx(model, out, wb)
        except PermissionError:
            pending.append(TM.write_workbook_xlsx(model, out, wb, os.path.join(out, wb, f'{wb}.xlsx.pending')))
        TM.write_workbook_csvs(model, out, wb)
    if not args.out:
        edit_scope(os.path.join(tables, 'scope.json'), args.phase)
    print('phase', args.phase, 'rewrote', changed)
    for p in pending:
        print('LOCKED: wrote', p, '— close Excel and move it over the original')


if __name__ == '__main__':
    main()
