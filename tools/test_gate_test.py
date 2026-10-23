"""Testy selektora zmian w test_gate.py (stdlib only)."""

import unittest

from test_gate import NON_SLOW_FILTER, TARGETS, filter_expression, select_changes


class TestGateSelectionTests(unittest.TestCase):
    def test_6809_change_selects_cpu_specific_tests_and_ir_shard(self):
        selection = select_changes(["src/CathodeRay.C/M6809Isa.cs"])

        self.assertEqual(selection.ir_targets, {"6809"})
        self.assertEqual(selection.matrix_targets, {"6809"})
        self.assertIn("M6809TargetTests", selection.classes)
        self.assertEqual(
            selection.matrix_classes, {"TargetMatrixTests", "VRegMatrixTests"}
        )
        expression = filter_expression(selection)
        assert expression is not None
        self.assertIn(
            "FullyQualifiedName~TargetMatrixTests&DisplayName~6809", expression
        )
        self.assertIn("FullyQualifiedName~VRegMatrixTests&DisplayName~6809", expression)

    def test_shared_selector_checks_all_matrix_targets(self):
        selection = select_changes(["src/CathodeRay.C/ByteSelector.Call.cs"])

        self.assertTrue(selection.all_matrix_targets)
        self.assertEqual(selection.ir_targets, set(TARGETS))
        self.assertEqual(
            selection.matrix_classes, {"TargetMatrixTests", "VRegMatrixTests"}
        )

    def test_ir_helper_change_selects_all_callers_and_targets(self):
        selection = select_changes(["tests/CathodeRay.Tests/IrProgram.cs"])

        self.assertEqual(selection.ir_targets, set(TARGETS))
        self.assertTrue(
            {"RandomIrTests", "RegisterAllocatorTests"}.issubset(selection.classes)
        )

    def test_docs_only_change_needs_no_code_tests(self):
        selection = select_changes(["docs/codegen/opt-6809.md"])

        self.assertIsNone(filter_expression(selection))
        self.assertFalse(selection.full_fallback)

    def test_unknown_source_uses_full_fallback(self):
        selection = select_changes(["src/CathodeRay.C/NewPass.cs"])

        self.assertTrue(selection.full_fallback)

    def test_fuzz_generator_runs_smoke_not_noop_test(self):
        selection = select_changes(["tools/leaf_fuzz.py"])

        self.assertEqual(selection.fuzz, {"leaf"})
        self.assertIsNone(filter_expression(selection))

    def test_workflow_and_root_markdown_changes_need_no_tests(self):
        for path in (".github/workflows/ci.yml", "README.md", "CLAUDE.md"):
            selection = select_changes([path])

            self.assertFalse(selection.full_fallback, path)
            self.assertIsNone(filter_expression(selection), path)

    def test_compat_sample_runs_only_its_runner_class(self):
        selection = select_changes(["samples/compat/b2_block_scope.c"])

        self.assertFalse(selection.full_fallback)
        self.assertEqual(selection.classes, {"CompatSamplesTests"})
        self.assertFalse(selection.matrix_classes)
        self.assertFalse(selection.ir_targets)

    def test_frontend_change_runs_compiler_tests_and_guards_but_no_backend_matrix(self):
        selection = select_changes(["src/CathodeRay.C/Parser.cs"])

        self.assertFalse(selection.full_fallback)
        self.assertFalse(selection.matrix_classes)
        self.assertFalse(selection.ir_targets)
        self.assertIn("CFrontendTests", selection.classes)
        self.assertTrue({"IrGateTests", "TargetSizeTests", "VRegSizeTests"}.issubset(selection.classes))
        expression = filter_expression(selection)
        assert expression is not None
        self.assertNotIn("TargetMatrixTests", expression)

    def test_stdlib_and_typechecker_use_the_frontend_set(self):
        for path in ("src/CathodeRay.C/stdlib/lib/printf.c", "src/CathodeRay.C/TypeChecker.Flow.cs", "src/CathodeRay.C/CType.cs"):
            selection = select_changes([path])

            self.assertFalse(selection.full_fallback, path)
            self.assertIn("CFrontendTests", selection.classes, path)

    def test_cli_change_runs_tests_that_call_the_cli(self):
        selection = select_changes(["src/CathodeRay.Cli/CcCommand.cs"])

        self.assertFalse(selection.full_fallback)
        self.assertIn("CCliTests", selection.classes)
        self.assertFalse(selection.matrix_classes)

    def test_assembler_and_emulator_changes_run_the_suite_without_slow_matrices(self):
        for path in ("src/CathodeRay.Assembler/TwoPassAssembler.cs", "src/CathodeRay.Emulation/Z80Cpu.cs", "src/CathodeRay/Stub/StubCpu.cs"):
            selection = select_changes([path])

            self.assertFalse(selection.full_fallback, path)
            self.assertEqual(filter_expression(selection), NON_SLOW_FILTER, path)

    def test_shared_lowering_and_build_files_still_fall_back_to_the_full_flow(self):
        for path in ("src/CathodeRay.C/Lowering.Calls.cs", "Directory.Build.props", "global.json"):
            self.assertTrue(select_changes([path]).full_fallback, path)


if __name__ == "__main__":
    unittest.main()
