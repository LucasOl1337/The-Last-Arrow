import argparse
import contextlib
import json
import sqlite3
from collections import Counter
from pathlib import Path
from typing import Any


DEFAULT_DB_PATH = Path(".codegraph") / "codegraph.db"
TIMESTAMP_MILLISECONDS_THRESHOLD = 10_000_000_000
TIMESTAMP_TOLERANCE_SECONDS = 1.0
PYTHON_BUILTIN_REFERENCES = {
    "abs",
    "all",
    "any",
    "bool",
    "dict",
    "enumerate",
    "Exception",
    "FileNotFoundError",
    "float",
    "int",
    "isinstance",
    "len",
    "list",
    "max",
    "min",
    "print",
    "range",
    "round",
    "set",
    "sorted",
    "staticmethod",
    "str",
    "sum",
    "OSError",
    "TypeError",
    "tuple",
    "ValueError",
}
PYTHON_EXTERNAL_ROOTS = {
    "argparse",
    "contextlib",
    "deepcopy",
    "json",
    "os",
    "Path",
    "sqlite3",
    "sys",
    "tempfile",
    "time",
    "unittest",
}
PYTHON_EXTERNAL_ROOTS_LOWER = {name.lower() for name in PYTHON_EXTERNAL_ROOTS}
PYTHON_NOISY_MEMBER_NAMES = {
    "append",
    "assertEqual",
    "assertIn",
    "assertLess",
    "assertTrue",
    "extend",
    "get",
    "items",
    "join",
    "keys",
    "lower",
    "execute",
    "fetchall",
    "fetchone",
    "commit",
    "close",
    "exists",
    "mkdir",
    "read_text",
    "relative_to",
    "replace",
    "resolve",
    "setdefault",
    "sort",
    "split",
    "splitlines",
    "stat",
    "strip",
    "unlink",
    "upper",
    "values",
    "write_text",
}
CSHARP_EXTERNAL_ROOTS = {
    "Assert",
    "Bounds",
    "Camera",
    "Collider2D",
    "Color",
    "Debug",
    "Dictionary",
    "Enumerable",
    "GameObject",
    "Gizmos",
    "GL",
    "HashSet",
    "IEnumerable",
    "IReadOnlyList",
    "Input",
    "Is",
    "JsonUtility",
    "KeyCode",
    "Keyboard",
    "LegacyInput",
    "List",
    "Mathf",
    "MonoBehaviour",
    "NUnit",
    "Object",
    "Path",
    "Physics2D",
    "Quaternion",
    "Random",
    "Rect",
    "Resources",
    "Rigidbody2D",
    "ScriptableObject",
    "SpriteRenderer",
    "StringBuilder",
    "Time",
    "Transform",
    "Vector2",
    "Vector3",
    "string",
}
CSHARP_NOISY_MEMBER_NAMES = {
    "Contains",
    "DrawWireCube",
    "DrawWireSphere",
    "GetComponent",
    "GetProperty",
    "GetType",
    "GetValue",
    "Invoke",
    "SetValue",
    "StartCoroutine",
}


def collect_codegraph_report(
    db_path: Path,
    limit: int = 10,
    workspace_root: Path | None = None,
) -> dict[str, Any]:
    if not db_path.exists():
        raise FileNotFoundError(f"CodeGraphy database not found: {db_path}")

    resolved_workspace_root = resolve_workspace_root(db_path, workspace_root)
    with contextlib.closing(sqlite3.connect(db_path)) as connection:
        connection.row_factory = sqlite3.Row
        tables = list_tables(connection)
        return {
            "dbPath": str(db_path),
            "workspaceRoot": str(resolved_workspace_root),
            "tables": tables,
            "counts": collect_counts(connection, tables),
            "staleness": collect_staleness(connection, tables, resolved_workspace_root, limit),
            "topFilesByUnresolvedRefs": collect_top_files_by_unresolved_refs(
                connection,
                tables,
                limit,
            ),
            "topUnresolvedReferences": collect_top_unresolved_references(
                connection,
                tables,
                limit,
            ),
            "unresolvedReferenceNoise": collect_unresolved_reference_noise(connection, tables),
            "topProjectFilesByUnresolvedRefs": collect_top_project_files_by_unresolved_refs(
                connection,
                tables,
                limit,
            ),
            "topProjectUnresolvedReferences": collect_top_project_unresolved_references(
                connection,
                tables,
                limit,
            ),
            "topFilesByNodeCount": collect_top_files_by_node_count(connection, limit),
            "topNodesByDegree": collect_top_nodes_by_degree(connection, limit),
        }


def resolve_workspace_root(db_path: Path, workspace_root: Path | None) -> Path:
    if workspace_root is not None:
        return workspace_root.resolve()

    resolved_db_path = db_path.resolve()
    if resolved_db_path.parent.name == ".codegraph":
        return resolved_db_path.parent.parent
    return resolved_db_path.parent


def list_tables(connection: sqlite3.Connection) -> list[str]:
    rows = connection.execute(
        "select name from sqlite_master where type = 'table' order by name"
    ).fetchall()
    return [str(row["name"]) for row in rows]


def list_columns(connection: sqlite3.Connection, table: str) -> set[str]:
    rows = connection.execute(f'pragma table_info("{table}")').fetchall()
    return {str(row["name"]) for row in rows}


def collect_counts(connection: sqlite3.Connection, tables: list[str]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for table in ("files", "nodes", "edges", "unresolved_refs"):
        if table not in tables:
            counts[table] = 0
            continue
        counts[table] = int(connection.execute(f'select count(*) from "{table}"').fetchone()[0])
    return counts


def collect_staleness(
    connection: sqlite3.Connection,
    tables: list[str],
    workspace_root: Path,
    limit: int,
) -> dict[str, Any]:
    summary: dict[str, Any] = {
        "checkedFiles": 0,
        "staleFiles": 0,
        "missingFiles": 0,
        "staleHotspots": [],
    }

    if "files" not in tables:
        summary["status"] = "files_table_missing"
        return summary

    required_columns = {"path", "size", "modified_at", "indexed_at"}
    if not required_columns.issubset(list_columns(connection, "files")):
        summary["status"] = "timestamp_columns_missing"
        return summary

    rows = connection.execute(
        """
        select path, size, modified_at, indexed_at
        from files
        order by path asc
        """
    ).fetchall()

    hotspot_limit = max(1, limit)
    for row in rows:
        summary["checkedFiles"] += 1
        stale_entry = build_stale_file_entry(row, workspace_root)
        if stale_entry is None:
            continue

        summary["staleFiles"] += 1
        if "missing" in stale_entry["reasons"]:
            summary["missingFiles"] += 1
        if len(summary["staleHotspots"]) < hotspot_limit:
            summary["staleHotspots"].append(stale_entry)

    return summary


def build_stale_file_entry(row: sqlite3.Row, workspace_root: Path) -> dict[str, Any] | None:
    path = str(row["path"])
    db_size = parse_optional_int(row["size"])
    db_modified_at = normalize_epoch_seconds(row["modified_at"])
    indexed_at = normalize_epoch_seconds(row["indexed_at"])
    reasons: list[str] = []

    disk_path = resolve_indexed_path(workspace_root, path)
    disk_size: int | None = None
    disk_modified_at: float | None = None

    if disk_path is None:
        reasons.append("invalid_path")
    elif not disk_path.exists():
        reasons.append("missing")
    else:
        stat = disk_path.stat()
        disk_size = stat.st_size
        disk_modified_at = stat.st_mtime
        if db_size is not None and disk_size != db_size:
            reasons.append("size_changed")
        if indexed_at is not None and disk_modified_at > indexed_at + TIMESTAMP_TOLERANCE_SECONDS:
            reasons.append("modified_after_index")
        elif (
            db_modified_at is not None
            and disk_modified_at > db_modified_at + TIMESTAMP_TOLERANCE_SECONDS
        ):
            reasons.append("modified_after_record")

    if not reasons:
        return None

    return {
        "path": path,
        "reasons": reasons,
        "dbSize": db_size,
        "diskSize": disk_size,
        "dbModifiedAt": db_modified_at,
        "indexedAt": indexed_at,
        "diskModifiedAt": disk_modified_at,
    }


def parse_optional_int(value: Any) -> int | None:
    if value is None:
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def normalize_epoch_seconds(value: Any) -> float | None:
    if value is None:
        return None
    try:
        timestamp = float(value)
    except (TypeError, ValueError):
        return None
    if timestamp > TIMESTAMP_MILLISECONDS_THRESHOLD:
        return timestamp / 1000.0
    return timestamp


def resolve_indexed_path(workspace_root: Path, indexed_path: str) -> Path | None:
    raw_path = Path(indexed_path)
    candidate = raw_path if raw_path.is_absolute() else workspace_root / raw_path
    resolved_candidate = candidate.resolve(strict=False)
    try:
        resolved_candidate.relative_to(workspace_root)
    except ValueError:
        return None
    return resolved_candidate


def collect_top_files_by_node_count(connection: sqlite3.Connection, limit: int) -> list[dict[str, Any]]:
    rows = connection.execute(
        """
        select path, language, size, node_count
        from files
        order by node_count desc, size desc, path asc
        limit ?
        """,
        (max(1, limit),),
    ).fetchall()
    return [dict(row) for row in rows]


def collect_top_files_by_unresolved_refs(
    connection: sqlite3.Connection,
    tables: list[str],
    limit: int,
) -> list[dict[str, Any]]:
    if "unresolved_refs" not in tables:
        return []
    required_columns = {"file_path", "language"}
    if not required_columns.issubset(list_columns(connection, "unresolved_refs")):
        return []

    rows = connection.execute(
        """
        select
            file_path,
            language,
            count(*) as unresolved_count
        from unresolved_refs
        group by file_path, language
        order by unresolved_count desc, file_path asc
        limit ?
        """,
        (max(1, limit),),
    ).fetchall()
    return [dict(row) for row in rows]


def collect_top_unresolved_references(
    connection: sqlite3.Connection,
    tables: list[str],
    limit: int,
) -> list[dict[str, Any]]:
    if "unresolved_refs" not in tables:
        return []
    required_columns = {"reference_name", "reference_kind"}
    if not required_columns.issubset(list_columns(connection, "unresolved_refs")):
        return []

    rows = connection.execute(
        """
        select
            reference_name,
            reference_kind,
            count(*) as count
        from unresolved_refs
        group by reference_name, reference_kind
        order by count desc, reference_name asc
        limit ?
        """,
        (max(1, limit),),
    ).fetchall()
    return [dict(row) for row in rows]


def collect_unresolved_reference_noise(connection: sqlite3.Connection, tables: list[str]) -> dict[str, int]:
    rows = fetch_unresolved_reference_rows(connection, tables)
    likely_external = sum(
        1
        for row in rows
        if is_likely_external_unresolved_reference(row.get("reference_name", ""), row.get("language", ""))
    )
    return {
        "total": len(rows),
        "likelyExternal": likely_external,
        "potentiallyProject": len(rows) - likely_external,
    }


def collect_top_project_files_by_unresolved_refs(
    connection: sqlite3.Connection,
    tables: list[str],
    limit: int,
) -> list[dict[str, Any]]:
    rows = [
        row
        for row in fetch_unresolved_reference_rows(connection, tables)
        if not is_likely_external_unresolved_reference(row.get("reference_name", ""), row.get("language", ""))
    ]
    counter: Counter[tuple[str, str]] = Counter(
        (str(row.get("file_path", "")), str(row.get("language", "")))
        for row in rows
    )
    items = [
        {"file_path": file_path, "language": language, "unresolved_count": count}
        for (file_path, language), count in counter.items()
    ]
    items.sort(key=lambda item: (-int(item["unresolved_count"]), str(item["file_path"])))
    return items[: max(1, limit)]


def collect_top_project_unresolved_references(
    connection: sqlite3.Connection,
    tables: list[str],
    limit: int,
) -> list[dict[str, Any]]:
    rows = [
        row
        for row in fetch_unresolved_reference_rows(connection, tables)
        if not is_likely_external_unresolved_reference(row.get("reference_name", ""), row.get("language", ""))
    ]
    counter: Counter[tuple[str, str]] = Counter(
        (str(row.get("reference_name", "")), str(row.get("reference_kind", "")))
        for row in rows
    )
    items = [
        {"reference_name": reference_name, "reference_kind": reference_kind, "count": count}
        for (reference_name, reference_kind), count in counter.items()
    ]
    items.sort(key=lambda item: (-int(item["count"]), str(item["reference_name"])))
    return items[: max(1, limit)]


def fetch_unresolved_reference_rows(connection: sqlite3.Connection, tables: list[str]) -> list[dict[str, Any]]:
    if "unresolved_refs" not in tables:
        return []
    required_columns = {"reference_name", "reference_kind", "file_path", "language"}
    if not required_columns.issubset(list_columns(connection, "unresolved_refs")):
        return []

    rows = connection.execute(
        """
        select reference_name, reference_kind, file_path, language
        from unresolved_refs
        """
    ).fetchall()
    return [dict(row) for row in rows]


def is_likely_external_unresolved_reference(reference_name: Any, language: Any) -> bool:
    normalized_name = str(reference_name or "").strip()
    if not normalized_name:
        return False

    normalized_language = str(language or "").strip().lower()
    root_name = normalized_name.split(".", 1)[0]
    member_name = normalized_name.rsplit(".", 1)[-1]

    if normalized_language == "python":
        return (
            normalized_name in PYTHON_BUILTIN_REFERENCES
            or root_name in PYTHON_EXTERNAL_ROOTS
            or root_name.lower() in PYTHON_EXTERNAL_ROOTS_LOWER
            or member_name in PYTHON_NOISY_MEMBER_NAMES
        )

    if normalized_language == "csharp":
        return (
            root_name in CSHARP_EXTERNAL_ROOTS
            or member_name in CSHARP_NOISY_MEMBER_NAMES
            or (normalized_name.startswith("(") and normalized_name.endswith(")"))
        )

    return False


def collect_top_nodes_by_degree(connection: sqlite3.Connection, limit: int) -> list[dict[str, Any]]:
    rows = connection.execute(
        """
        with degree as (
            select source as node_id, count(*) as out_degree, 0 as in_degree
            from edges
            group by source
            union all
            select target as node_id, 0 as out_degree, count(*) as in_degree
            from edges
            group by target
        ),
        collapsed as (
            select
                node_id,
                sum(out_degree) as out_degree,
                sum(in_degree) as in_degree
            from degree
            group by node_id
        )
        select
            n.kind,
            n.qualified_name,
            n.file_path,
            n.start_line,
            coalesce(c.out_degree, 0) as out_degree,
            coalesce(c.in_degree, 0) as in_degree,
            coalesce(c.out_degree, 0) + coalesce(c.in_degree, 0) as degree
        from collapsed c
        join nodes n on n.id = c.node_id
        order by degree desc, n.qualified_name asc
        limit ?
        """,
        (max(1, limit),),
    ).fetchall()
    return [dict(row) for row in rows]


def render_markdown(report: dict[str, Any]) -> str:
    counts = report.get("counts", {})
    staleness = report.get("staleness", {})
    lines = [
        "# CodeGraphy Architecture Report",
        "",
        f"Database: `{report.get('dbPath', '')}`",
        f"Workspace: `{report.get('workspaceRoot', '')}`",
        "",
        "## Counts",
        "",
        f"- files: {counts.get('files', 0)}",
        f"- nodes: {counts.get('nodes', 0)}",
        f"- edges: {counts.get('edges', 0)}",
        f"- unresolved refs: {counts.get('unresolved_refs', 0)}",
        "",
        "## Index Freshness",
        "",
        f"- checked files: {staleness.get('checkedFiles', 0)}",
        f"- stale files: {staleness.get('staleFiles', 0)}",
        f"- missing files: {staleness.get('missingFiles', 0)}",
    ]

    stale_hotspots = staleness.get("staleHotspots", [])
    if stale_hotspots:
        lines.extend(["", "### Stale Hotspots", ""])
        for item in stale_hotspots:
            reasons = ", ".join(item.get("reasons", []))
            lines.append(
                "- "
                f"{reasons} | "
                f"db {format_size(item.get('dbSize'))} -> disk {format_size(item.get('diskSize'))} | "
                f"`{item.get('path', '')}`"
            )

    lines.extend(["", "## Unresolved References", ""])
    lines.append(f"- total: {counts.get('unresolved_refs', 0)}")
    unresolved_noise = report.get("unresolvedReferenceNoise", {})
    if unresolved_noise:
        lines.append(f"- likely external/noisy refs: {unresolved_noise.get('likelyExternal', 0)}")
        lines.append(f"- potential project refs: {unresolved_noise.get('potentiallyProject', 0)}")

    top_project_files = report.get("topProjectFilesByUnresolvedRefs", [])
    if top_project_files:
        lines.extend(["", "### Top Potential Project Files By Unresolved References", ""])
        for item in top_project_files:
            lines.append(
                "- "
                f"{item.get('unresolved_count', 0)} refs | "
                f"{item.get('language', '')} | "
                f"`{item.get('file_path', '')}`"
            )

    top_project_refs = report.get("topProjectUnresolvedReferences", [])
    if top_project_refs:
        lines.extend(["", "### Top Potential Project Reference Names", ""])
        for item in top_project_refs:
            lines.append(
                "- "
                f"{item.get('count', 0)} refs | "
                f"{item.get('reference_kind', '')} | "
                f"`{item.get('reference_name', '')}`"
            )

    top_unresolved_files = report.get("topFilesByUnresolvedRefs", [])
    if top_unresolved_files:
        lines.extend(["", "### Top Files By Unresolved References", ""])
        for item in top_unresolved_files:
            lines.append(
                "- "
                f"{item.get('unresolved_count', 0)} refs | "
                f"{item.get('language', '')} | "
                f"`{item.get('file_path', '')}`"
            )

    top_unresolved_refs = report.get("topUnresolvedReferences", [])
    if top_unresolved_refs:
        lines.extend(["", "### Top Unresolved Reference Names", ""])
        for item in top_unresolved_refs:
            lines.append(
                "- "
                f"{item.get('count', 0)} refs | "
                f"{item.get('reference_kind', '')} | "
                f"`{item.get('reference_name', '')}`"
            )

    lines.extend(["", "## Top Files By Node Count", ""])
    for item in report.get("topFilesByNodeCount", []):
        lines.append(
            f"- {item.get('node_count', 0)} nodes | {item.get('size', 0)} bytes | `{item.get('path', '')}`"
        )

    lines.extend(["", "## Top Nodes By Edge Degree", ""])
    for item in report.get("topNodesByDegree", []):
        lines.append(
            "- "
            f"{item.get('degree', 0)} degree "
            f"({item.get('out_degree', 0)} out/{item.get('in_degree', 0)} in) | "
            f"{item.get('kind', '')} | `{item.get('qualified_name', '')}` | "
            f"`{item.get('file_path', '')}:{item.get('start_line', 0)}`"
        )

    return "\n".join(lines) + "\n"


def format_size(value: Any) -> str:
    if value is None:
        return "missing"
    return f"{value} bytes"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate a report from a local CodeGraphy SQLite database.")
    parser.add_argument("--db", type=Path, default=DEFAULT_DB_PATH, help="Path to .codegraph/codegraph.db")
    parser.add_argument("--workspace-root", type=Path, help="Workspace root used to validate indexed files")
    parser.add_argument("--limit", type=int, default=10, help="Number of hotspots to include")
    parser.add_argument("--json", action="store_true", help="Emit JSON instead of Markdown")
    args = parser.parse_args(argv)

    report = collect_codegraph_report(args.db, args.limit, args.workspace_root)
    if args.json:
        print(json.dumps(report, ensure_ascii=True, indent=2))
    else:
        print(render_markdown(report), end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
