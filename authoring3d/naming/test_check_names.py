"""Single portable component: synthetic vocabulary and files live only in disposable system temp directories."""
import io
import contextlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

import check_names


class NamingCheckTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='cthangover-naming-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def write(self, name, text, encoding='utf-8'):
        path = self.root/name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding=encoding)
        return path

    def test_case_insensitive_literal_columns_and_multiple_occurrences(self):
        self.write('docs/notes.md', 'ok\nXX bLoCkEd.ToKeN and blocked.token\nblockedXtoken')
        result = check_names.scan(self.root, ['Blocked.Token'])
        self.assertEqual([(2, 4), (2, 22)], [(r['line'], r['column']) for r in result['findings']])
        self.assertEqual('failed', result['status'])
        self.assertEqual(1, check_names.exit_code(result))

    def test_utf8_bom_and_cyrillic(self):
        self.write('docs/notes.md', 'Пример\nЗАПРЕТНАЯ_МЕТКА', 'utf-8-sig')
        result = check_names.scan(self.root, ['запретная_метка'])
        self.assertEqual((2, 1, 'ЗАПРЕТНАЯ_МЕТКА'), (result['findings'][0]['line'], result['findings'][0]['column'], result['findings'][0]['match']))

    def test_utf16_and_utf32_are_text(self):
        self.write('docs/wide.txt', 'first\nblocked_label', 'utf-16')
        self.write('docs/wider.txt', 'first\nblocked_label', 'utf-32')
        result = check_names.scan(self.root, ['blocked_label'])
        self.assertEqual(2, len(result['findings']))
        self.assertEqual([2, 2], [row['line'] for row in result['findings']])
        self.assertEqual(0, result['binary_files'])

    def test_gitignored_and_active_workstream_source_is_checked(self):
        self.write('.gitignore', 'mods_private/\n')
        self.write('mods_private/example/src/sample.cs', '// blocked_label')
        self.write('mods_private/combat3d_base/tests/example.json', '{"value":"blocked_label"}')
        result = check_names.scan(self.root, ['blocked_label'])
        self.assertEqual(2, len(result['findings']))

    def test_metadata_and_generated_outputs_are_outside_source_scope(self):
        for name in ['.git/config', 'mods_private/example/.git/config', '.godot/cache.txt',
            'tests/artifacts/capture.txt', 'gen/source.txt', 'jobs/old.ps1', 'releases/notes.txt',
            'sdk/vendor.txt', '.claude/skills/alias.md', '.dsh/skills/alias.md', 'authoring3d/__pycache__/old.pyc', 'site/node_modules/vendor.js', 'build_site.log']:
            self.write(name, 'blocked_label')
        result = check_names.scan(self.root, ['blocked_label'])
        self.assertEqual([], result['findings'])
        self.assertEqual('passed', result['status'])

    def test_binary_and_directory_names_are_checked(self):
        path = self.root/'mods/example/blocked_label.png'
        path.parent.mkdir(parents=True)
        path.write_bytes(b'\x00blocked_label')
        self.write('docs/blocked_label_folder/neutral.md', 'clean')
        result = check_names.scan(self.root, ['blocked_label'])
        self.assertEqual(2, len(result['findings']))
        self.assertTrue(all(row['kind'] == 'path' and row['line'] == 0 for row in result['findings']))
        self.assertEqual(1, result['binary_files'])

    def test_invalid_text_cannot_produce_a_clean_result(self):
        self.write('Core/source.cs', 'clean')
        (self.root/'Core/source.cs').write_bytes(b'bad\x00source')
        result = check_names.scan(self.root, ['blocked_label'])
        self.assertEqual('error', result['status'])
        self.assertEqual(2, check_names.exit_code(result))
        (self.root/'Core/source.cs').write_bytes(b'\xff\xfd')
        self.assertEqual('error', check_names.scan(self.root, ['blocked_label'])['status'])

    def test_core_comment_exception_does_not_hide_literals_other_mods_or_neighbouring_words(self):
        phrase = check_names.ALLOWED_CORE_COMMENT.pattern.replace(r'\b', '')
        term = phrase.split()[-1]
        self.write('Core/Allowed.cs', '/// '+phrase+'\n/// '+phrase+'; blocked_label')
        self.write('Core/Literal.cs', 'var message = "'+phrase+'";')
        self.write('mods/example/src/Comment.cs', '// '+phrase)
        result = check_names.scan(self.root, [term, 'blocked_label'])
        self.assertEqual(['Core/Allowed.cs', 'Core/Literal.cs', 'mods/example/src/Comment.cs'], [r['path'] for r in result['findings']])
        self.assertEqual('blocked_label', result['findings'][0]['match'])
        self.assertEqual(len('/// '+phrase+'; ')+1, result['findings'][0]['column'])

    def test_chunk_boundary_and_unicode_casefold_preserve_positions(self):
        prefix = 'x'*(262144-4)
        self.write('docs/large.txt', prefix+'blocked_label\nß BLOCKED_LABEL')
        result = check_names.scan(self.root, ['blocked_label'])
        self.assertEqual([(1, len(prefix)+1), (2, 3)], [(r['line'], r['column']) for r in result['findings']])

    def test_symlinks_are_named_but_never_followed(self):
        target = self.write('external.txt', 'blocked_label')
        directory = self.root/'docs'
        directory.mkdir()
        try:
            (directory/'blocked_label_link').symlink_to(target)
        except (OSError, NotImplementedError):
            self.skipTest('Native symlinks are unavailable on this host')
        target.unlink()
        result = check_names.scan(self.root, ['blocked_label'])
        self.assertEqual(1, result['symlinks_skipped'])
        self.assertEqual('path', result['findings'][0]['kind'])

    def test_external_list_is_the_only_authority_and_missing_list_is_an_explicit_skip(self):
        with mock.patch.object(check_names, 'TERMS_PATH') as external:
            external.open.return_value.__enter__.return_value = io.StringIO('# comment\n Blocked_Label \n\nblocked_label\n')
            self.assertEqual(['blocked_label'], check_names.load_terms())
            external.open.assert_called_once_with(encoding='utf-8-sig')
        with mock.patch.object(check_names, 'load_terms', side_effect=FileNotFoundError()):
            result = check_names.run(self.root)
        self.assertEqual('skipped', result['status'])
        self.assertIn('missing', result['warning'])
        self.assertEqual(0, check_names.exit_code(result))
        with mock.patch.object(check_names, 'load_terms', return_value=[]):
            self.assertEqual('skipped', check_names.run(self.root)['status'])
        with mock.patch.object(check_names, 'load_terms', side_effect=PermissionError('denied')):
            self.assertEqual('error', check_names.run(self.root)['status'])

    def test_cli_missing_list_warns_and_json_says_skipped(self):
        output, errors = io.StringIO(), io.StringIO()
        with mock.patch.object(check_names, 'load_terms', side_effect=FileNotFoundError()), \
             mock.patch('sys.argv', ['check_names.py', str(self.root), '--json']), \
             contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            code = check_names.main()
        self.assertEqual(0, code)
        self.assertEqual('skipped', json.loads(output.getvalue())['status'])
        self.assertIn('WARNING:', errors.getvalue())

    def test_cli_cannot_override_the_external_list(self):
        errors = io.StringIO()
        with mock.patch('sys.argv', ['check_names.py', str(self.root), '--terms', 'other.txt']), \
             contextlib.redirect_stderr(errors), self.assertRaises(SystemExit) as raised:
            check_names.main()
        self.assertEqual(2, raised.exception.code)

    def test_missing_root_fails_and_clean_root_passes(self):
        self.write('README.md', 'neutral wording')
        self.assertEqual('passed', check_names.scan(self.root, ['blocked_label'])['status'])
        self.assertEqual('error', check_names.scan(self.root/'missing', ['blocked_label'])['status'])


if __name__ == '__main__':
    unittest.main()
