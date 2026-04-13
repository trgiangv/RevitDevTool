"""Transaction tests — demonstrates creating and rolling back changes.

Shows how users can build their own transaction isolation patterns
without any framework-imposed behavior.
"""

def test_transaction_start_and_rollback(revit_doc):
    """A request-scoped Revit transaction can start and roll back cleanly."""
    from Autodesk.Revit.DB import Transaction, TransactionStatus

    tx = Transaction(revit_doc, "pytest: transaction smoke")
    assert tx.Start() == TransactionStatus.Started

    status = tx.RollBack()
    assert status == TransactionStatus.RolledBack
    print("Transaction start → rollback cycle OK")


def test_read_project_info(revit_doc):
    """Read project information — no transaction needed for read-only."""
    info = revit_doc.ProjectInformation
    assert info is not None
    print(f"Project name: {info.Name}")
    print(f"Project number: {info.Number}")
