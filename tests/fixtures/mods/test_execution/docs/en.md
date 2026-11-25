# Test execution fixtures

This fixture provides explicit TestRunner probes for process isolation, the bootstrap contract and editor writes. The `probe`, `bootstrap` and `editor_write` suites activate through their matching `--itest=test_execution_*` arguments.

Run the named integration case from the project’s TestRunner catalogue and read its report for actual workspace, output and process results. The editor-write case exercises saving through a writable fixture lane.
