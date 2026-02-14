import itertools
import time
import uuid

# If reset works correctly, this module is recreated every run.
SESSION_ID = uuid.uuid4().hex[:8]
IMPORTED_AT = time.time()
_RUN_COUNTER = itertools.count(1)


def next_run_id() -> int:
    return next(_RUN_COUNTER)
