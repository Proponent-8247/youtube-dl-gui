"""Apply exact, base-commit-pinned source edits. No commands are accepted from the plan."""
import argparse
import hashlib
import json
import re
from pathlib import Path, PurePosixPath

ROOT = Path.cwd().resolve()
ALLOWED_ROOTS = {'Addons', 'Controls', 'Languages', 'tests', 'youtube-dl-gui', 'youtube-dl-gui-updater'}
ALLOWED_SUFFIXES = {'.cs', '.csproj', '.js', '.projitems', '.resx', '.ini'}


def source_path(name):
    p = PurePosixPath(name)
    if (p.is_absolute() or not p.parts or p.parts[0] not in ALLOWED_ROOTS
            or '..' in p.parts or '\\' in name or ':' in name
            or p.suffix not in ALLOWED_SUFFIXES or str(p) != name
            or any(ord(c) < 32 for c in name)):
        raise ValueError('Disallowed source path: ' + name)
    target = ROOT.joinpath(*p.parts)
    if target.is_symlink() or any(x.is_symlink() for x in target.parents):
        raise ValueError('Symlink source path: ' + name)
    target.resolve().relative_to(ROOT)
    return target


def normalized_hash(data):
    return hashlib.sha256(data.replace(b'\r\n', b'\n')).hexdigest()


def apply_edits(name, data, edits):
    result = data
    for edit in edits:
        old = edit['old'].encode('utf-8')
        new = edit['new'].encode('utf-8')
        if not old or b'\r' in old or b'\r' in new:
            raise ValueError('Edits must contain nonempty LF-normalized anchors')
        expected_count = edit.get('count', 1)
        if not isinstance(expected_count, int) or expected_count < 1:
            raise ValueError('Invalid match count')
        if b'\n' not in old and b'\r\n' in result:
            new = new.replace(b'\n', b'\r\n')
        candidates = [(old, new)]
        if b'\n' in old:
            candidates.append((old.replace(b'\n', b'\r\n'), new.replace(b'\n', b'\r\n')))
        matches = [(a, b) for a, b in candidates if result.count(a) == expected_count]
        if len(matches) != 1:
            raise ValueError('Expected one unambiguous anchor variant in ' + name)
        a, b = matches[0]
        result = result.replace(a, b)
    return result


def apply_ini_write_before_cache_assignment(name, data, expected_count):
    if not isinstance(expected_count, int) or expected_count < 1:
        raise ValueError('Invalid config persistence transform count')

    sectioned = re.compile(
        rb'(?m)^(?P<indent>[ \t]+)(?P<field>f[A-Za-z_][A-Za-z0-9_]*) = value;'
        rb'(?P<newline>\r?\n)(?P=indent)IniProvider\.Write\('
        rb'(?P<property>[A-Za-z_][A-Za-z0-9_]*), ConfigName\);(?=\r?$)')
    unsectioned = re.compile(
        rb'(?m)^(?P<indent>[ \t]+)(?P<field>f[A-Za-z_][A-Za-z0-9_]*) = value;'
        rb'(?P<newline>\r?\n)(?P=indent)IniProvider\.Write\('
        rb'(?P<property>[A-Za-z_][A-Za-z0-9_]*)\);(?=\r?$)')

    def replacement(match, section):
        indent = match.group('indent')
        newline = match.group('newline')
        prop = match.group('property')
        write = (b'IniProvider.Write(value, ' + section + b', nameof(' + prop + b'));')
        return indent + write + newline + indent + match.group('field') + b' = value;'

    sectioned_matches = list(sectioned.finditer(data))
    unsectioned_matches = list(unsectioned.finditer(data))
    actual_count = len(sectioned_matches) + len(unsectioned_matches)
    if actual_count != expected_count:
        raise ValueError(
            'Expected {} config persistence assignments in {}; found {}'.format(
                expected_count, name, actual_count))

    result, sectioned_count = sectioned.subn(
        lambda match: replacement(match, b'ConfigName'), data)
    result, unsectioned_count = unsectioned.subn(
        lambda match: replacement(match, b'null'), result)
    if sectioned_count + unsectioned_count != expected_count:
        raise ValueError('Config persistence transform count changed while applying ' + name)
    return result


def apply_transforms(name, data, transforms):
    result = data
    for transform in transforms:
        transform_name = transform.get('name')
        if transform_name == 'ini_write_before_cache_assignment':
            result = apply_ini_write_before_cache_assignment(
                name, result, transform.get('count'))
        else:
            raise ValueError('Unsupported named transform: ' + str(transform_name))
    return result


def prepare(repair, state, version):
    if not re.fullmatch(r'[A-Za-z0-9_-]{1,64}', repair['id']):
        raise ValueError('Invalid repair id')
    changed = {}
    seen = set()
    for change in repair['files']:
        name = change['path']
        target = source_path(name)
        if name.casefold() in seen:
            raise ValueError('Duplicate source path: ' + name)
        seen.add(name.casefold())
        data = state[name] if name in state else (target.read_bytes() if target.exists() else None)

        if version == 1:
            expected = change['before_sha256']
            if expected is None:
                if data is not None:
                    raise ValueError('New file already exists: ' + name)
                result = change['content'].encode('utf-8')
            else:
                if data is None or normalized_hash(data) != expected:
                    raise ValueError('Source hash mismatch: ' + name)
                result = apply_edits(name, data, change['edits'])
        else:
            # Version 2 plans are only for existing files. The request's parent
            # commit pins the complete source tree, while exact anchors or a
            # constrained named transform pin each intended change within it.
            # The workflow separately refuses a superseded remote head and
            # pushes without force.
            if data is None:
                raise ValueError('Version 2 cannot create source files: ' + name)
            if 'content' in change or 'before_sha256' in change or 'after_sha256' in change:
                raise ValueError('Version 2 uses exact edits or named transforms, not per-file hashes: ' + name)
            has_edits = 'edits' in change
            has_transforms = 'transforms' in change
            if has_edits == has_transforms:
                raise ValueError('Version 2 change must specify exactly one of edits or transforms: ' + name)
            result = (apply_edits(name, data, change['edits']) if has_edits
                      else apply_transforms(name, data, change['transforms']))

        if result == data:
            raise ValueError('No-op edit: ' + name)
        if version == 1 and normalized_hash(result) != change['after_sha256']:
            raise ValueError('Result hash mismatch: ' + name)
        changed[name] = result
    state.update(changed)
    return changed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--preflight', action='store_true')
    parser.add_argument('--apply', type=int)
    args = parser.parse_args()
    plan = json.loads(Path('.audit-repairs.json').read_text(encoding='utf-8-sig'))
    version = plan.get('version')
    if version not in (1, 2) or not 1 <= len(plan['repairs']) <= 64:
        raise ValueError('Unsupported or empty repair plan')
    if args.preflight:
        state = {}
        for repair in plan['repairs']:
            prepare(repair, state, version)
            print('Preflight:', repair['id'])
    elif args.apply is not None:
        if not 0 <= args.apply < len(plan['repairs']):
            raise ValueError('Repair index is outside the plan')
        repair = plan['repairs'][args.apply]
        # Check every file before writing any file in this conceptual repair.
        changes = prepare(repair, {}, version)
        for name, data in changes.items():
            target = source_path(name)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        print('Applied:', repair['id'])
    else:
        parser.error('Choose --preflight or --apply INDEX')


if __name__ == '__main__':
    main()
