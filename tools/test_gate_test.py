"""Testy selektora zmian w test_gate.py (stdlib only)."""

import unittest

from test_gate import TARGETS, filter_expression, select_changes


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


if __name__ == "__main__":
    unittest.main()
