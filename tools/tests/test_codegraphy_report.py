import os
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parents[1]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

import codegraphy_report


class CodeGraphyReportTestCase(unittest.TestCase):
    def test_collect_codegraph_report_returns_counts_and_hotspots(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            db_path = Path(temp_dir) / "codegraph.db"
            create_sample_db(db_path)

            report = codegraphy_report.collect_codegraph_report(db_path, limit=2)

            self.assertEqual(2, report["counts"]["files"])
            self.assertEqual(3, report["counts"]["nodes"])
            self.assertEqual(3, report["counts"]["edges"])
            self.assertEqual(1, report["counts"]["unresolved_refs"])
            self.assertEqual("src/A.cs", report["topFilesByNodeCount"][0]["path"])
            self.assertEqual("A", report["topNodesByDegree"][0]["qualified_name"])
            self.assertEqual(
                "src/A.cs",
                report["topFilesByUnresolvedRefs"][0]["file_path"],
            )
            self.assertEqual(
                "Missing.Run",
                report["topUnresolvedReferences"][0]["reference_name"],
            )

    def test_collect_codegraph_report_classifies_likely_external_unresolved_refs(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            db_path = Path(temp_dir) / "codegraph.db"
            create_sample_db(db_path)
            append_unresolved_refs(
                db_path,
                [
                    ("n1", "Vector2", "instantiates", 5, 12, None, "src/A.cs", "csharp"),
                    ("n1", "str", "calls", 6, 4, None, "tools/a.py", "python"),
                    ("n1", "review.get", "calls", 7, 10, None, "tools/a.py", "python"),
                    ("n1", "Object.DestroyImmediate", "calls", 8, 6, None, "src/A.cs", "csharp"),
                    ("n1", "get", "calls", 9, 14, None, "tools/a.py", "python"),
                    ("n1", "GL.Vertex", "calls", 10, 16, None, "src/A.cs", "csharp"),
                    ("n1", "join", "calls", 11, 20, None, "tools/a.py", "python"),
                    ("n1", "staticmethod", "decorates", 12, 1, None, "tools/a.py", "python"),
                    ("n1", "connection.execute", "calls", 13, 20, None, "tools/a.py", "python"),
                    ("n1", "property.GetValue", "calls", 14, 24, None, "src/A.cs", "csharp"),
                    ("n1", "StartCoroutine", "calls", 15, 8, None, "src/A.cs", "csharp"),
                    ("n1", "(Vector3)", "calls", 16, 12, None, "src/A.cs", "csharp"),
                    ("n1", "method.Invoke", "calls", 17, 16, None, "src/A.cs", "csharp"),
                    ("n1", "Gizmos.DrawWireCube", "calls", 18, 16, None, "src/A.cs", "csharp"),
                    ("n1", "ValueError", "calls", 19, 16, None, "tools/a.py", "python"),
                    ("n1", "mkdir", "calls", 20, 16, None, "tools/a.py", "python"),
                    ("n1", "splitlines", "calls", 21, 16, None, "tools/a.py", "python"),
                ],
            )

            report = codegraphy_report.collect_codegraph_report(db_path, limit=5)

            self.assertEqual(
                {
                    "total": 18,
                    "likelyExternal": 17,
                    "potentiallyProject": 1,
                },
                report["unresolvedReferenceNoise"],
            )
            self.assertEqual(
                "Missing.Run",
                report["topProjectUnresolvedReferences"][0]["reference_name"],
            )
            self.assertEqual(
                "src/A.cs",
                report["topProjectFilesByUnresolvedRefs"][0]["file_path"],
            )

    def test_collect_codegraph_report_flags_stale_disk_files(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            db_path = root / "codegraph.db"
            workspace_root = root / "workspace"
            (workspace_root / "src").mkdir(parents=True)
            create_sample_db(db_path)

            base_time = 1_700_000_000.0
            connection = sqlite3.connect(db_path)
            try:
                connection.execute(
                    "update files set modified_at = ?, indexed_at = ?",
                    (int(base_time * 1000), int(base_time * 1000)),
                )
                connection.commit()
            finally:
                connection.close()

            fresh_file = workspace_root / "src" / "B.cs"
            fresh_file.write_text("b" * 100, encoding="utf-8")
            os.utime(fresh_file, (base_time - 10, base_time - 10))

            stale_file = workspace_root / "src" / "A.cs"
            stale_file.write_text("a" * 250, encoding="utf-8")
            os.utime(stale_file, (base_time + 10, base_time + 10))

            report = codegraphy_report.collect_codegraph_report(
                db_path,
                limit=2,
                workspace_root=workspace_root,
            )

            staleness = report["staleness"]
            self.assertEqual(2, staleness["checkedFiles"])
            self.assertEqual(1, staleness["staleFiles"])
            self.assertEqual(0, staleness["missingFiles"])
            self.assertEqual("src/A.cs", staleness["staleHotspots"][0]["path"])
            self.assertEqual(200, staleness["staleHotspots"][0]["dbSize"])
            self.assertEqual(250, staleness["staleHotspots"][0]["diskSize"])
            self.assertIn("size_changed", staleness["staleHotspots"][0]["reasons"])

    def test_render_markdown_includes_counts_and_locations(self) -> None:
        report = {
            "dbPath": ".codegraph/codegraph.db",
            "counts": {"files": 1, "nodes": 2, "edges": 3, "unresolved_refs": 2},
            "staleness": {
                "checkedFiles": 1,
                "staleFiles": 1,
                "missingFiles": 0,
                "staleHotspots": [
                    {
                        "path": "src/A.cs",
                        "reasons": ["size_changed"],
                        "dbSize": 90,
                        "diskSize": 100,
                    },
                ],
            },
            "topFilesByUnresolvedRefs": [
                {"file_path": "src/A.cs", "language": "csharp", "unresolved_count": 2},
            ],
            "topUnresolvedReferences": [
                {"reference_name": "Missing.Run", "reference_kind": "calls", "count": 2},
            ],
            "unresolvedReferenceNoise": {
                "total": 3,
                "likelyExternal": 1,
                "potentiallyProject": 2,
            },
            "topProjectFilesByUnresolvedRefs": [
                {"file_path": "src/A.cs", "language": "csharp", "unresolved_count": 2},
            ],
            "topProjectUnresolvedReferences": [
                {"reference_name": "Missing.Run", "reference_kind": "calls", "count": 2},
            ],
            "topFilesByNodeCount": [
                {"path": "src/A.cs", "size": 100, "node_count": 4},
            ],
            "topNodesByDegree": [
                {
                    "kind": "class",
                    "qualified_name": "A",
                    "file_path": "src/A.cs",
                    "start_line": 10,
                    "out_degree": 2,
                    "in_degree": 1,
                    "degree": 3,
                },
            ],
        }

        markdown = codegraphy_report.render_markdown(report)

        self.assertIn("files: 1", markdown)
        self.assertIn("stale files: 1", markdown)
        self.assertIn("size_changed", markdown)
        self.assertIn("- missing files: 0\n\n### Stale Hotspots", markdown)
        self.assertIn("## Unresolved References", markdown)
        self.assertIn("likely external/noisy refs: 1", markdown)
        self.assertIn("potential project refs: 2", markdown)
        self.assertIn("2 refs | csharp | `src/A.cs`", markdown)
        self.assertIn("2 refs | calls | `Missing.Run`", markdown)
        self.assertIn("### Top Potential Project Reference Names", markdown)
        self.assertLess(
            markdown.index("### Stale Hotspots"),
            markdown.index("## Top Files By Node Count"),
        )
        self.assertIn("`src/A.cs:10`", markdown)


def create_sample_db(db_path: Path) -> None:
    connection = sqlite3.connect(db_path)
    try:
        connection.executescript(
            """
            create table files (
                path text primary key,
                content_hash text not null,
                language text not null,
                size integer not null,
                modified_at integer not null,
                indexed_at integer not null,
                node_count integer
            );
            create table nodes (
                id text primary key,
                kind text not null,
                name text not null,
                qualified_name text not null,
                file_path text not null,
                language text not null,
                start_line integer not null,
                end_line integer not null,
                start_column integer not null,
                end_column integer not null
            );
            create table edges (
                id integer primary key,
                source text not null,
                target text not null,
                kind text not null
            );
            create table unresolved_refs (
                id integer primary key,
                from_node_id text not null,
                reference_name text not null,
                reference_kind text not null,
                line integer not null,
                col integer not null,
                candidates text,
                file_path text not null default '',
                language text not null default 'unknown'
            );
            """
        )
        connection.executemany(
            "insert into files values (?, ?, ?, ?, ?, ?, ?)",
            [
                ("src/A.cs", "hash-a", "csharp", 200, 0, 0, 3),
                ("src/B.cs", "hash-b", "csharp", 100, 0, 0, 1),
            ],
        )
        connection.executemany(
            "insert into nodes values (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            [
                ("n1", "class", "A", "A", "src/A.cs", "csharp", 1, 10, 0, 0),
                ("n2", "method", "Run", "A.Run", "src/A.cs", "csharp", 3, 8, 0, 0),
                ("n3", "class", "B", "B", "src/B.cs", "csharp", 1, 5, 0, 0),
            ],
        )
        connection.executemany(
            "insert into edges(source, target, kind) values (?, ?, ?)",
            [
                ("n1", "n2", "contains"),
                ("n1", "n3", "uses"),
                ("n3", "n1", "uses"),
            ],
        )
        connection.execute(
            """
            insert into unresolved_refs(
                from_node_id,
                reference_name,
                reference_kind,
                line,
                col,
                candidates,
                file_path,
                language
            )
            values (?, ?, ?, ?, ?, ?, ?, ?)
            """,
            ("n1", "Missing.Run", "calls", 4, 8, None, "src/A.cs", "csharp"),
        )
        connection.commit()
    finally:
        connection.close()


def append_unresolved_refs(db_path: Path, rows: list[tuple[str, str, str, int, int, str | None, str, str]]) -> None:
    connection = sqlite3.connect(db_path)
    try:
        connection.executemany(
            """
            insert into unresolved_refs(
                from_node_id,
                reference_name,
                reference_kind,
                line,
                col,
                candidates,
                file_path,
                language
            )
            values (?, ?, ?, ?, ?, ?, ?, ?)
            """,
            rows,
        )
        connection.commit()
    finally:
        connection.close()


if __name__ == "__main__":
    unittest.main()
