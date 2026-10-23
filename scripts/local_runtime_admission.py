"""Cross-checkout advisory admission for cooperating Timesheets runtime probes.

The canonical file is intentionally retained after release. No PID, checkout,
credentials, or other holder details are read or emitted.
"""

import os
from pathlib import Path
import stat

try:
    import fcntl
except ImportError:
    fcntl = None


LOCK_PATH = Path("/tmp/hexalith-timesheets-local-runtime.lock")


class RuntimeAdmissionError(RuntimeError):
    """A privacy-safe refusal before runtime admission."""


class LocalRuntimeAdmission:
    """Hold one nonblocking OS lock until cleanup and preservation finish."""

    def __init__(self):
        self.descriptor = None

    def __enter__(self):
        if (fcntl is None or not callable(getattr(fcntl, "flock", None))
                or not all(hasattr(os, flag) for flag in ("O_NOFOLLOW", "O_CLOEXEC", "O_NONBLOCK"))):
            raise RuntimeAdmissionError("Local runtime admission locking is unavailable; no resources started")
        descriptor = None
        try:
            if LOCK_PATH.parent != Path("/tmp") or Path("/tmp").resolve() != Path("/tmp"):
                raise OSError()
            descriptor = os.open(LOCK_PATH, os.O_CREAT | os.O_RDWR | os.O_NONBLOCK | os.O_NOFOLLOW | os.O_CLOEXEC, 0o600)
            actual = os.fstat(descriptor)
            named = LOCK_PATH.lstat()
            if (not stat.S_ISREG(actual.st_mode) or actual.st_uid != os.geteuid()
                    or actual.st_nlink != 1 or actual.st_mode & 0o077
                    or (actual.st_dev, actual.st_ino) != (named.st_dev, named.st_ino)):
                raise OSError()
            fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
            named = LOCK_PATH.lstat()
            if (actual.st_dev, actual.st_ino) != (named.st_dev, named.st_ino):
                raise OSError()
            self.descriptor = descriptor
            return self
        except OSError:
            if descriptor is not None:
                os.close(descriptor)
            raise RuntimeAdmissionError("Local runtime admission is busy or unsafe; no resources started") from None

    def __exit__(self, *_):
        if self.descriptor is not None:
            os.close(self.descriptor)
            self.descriptor = None
