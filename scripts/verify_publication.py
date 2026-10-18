"""Official NuGet v3 index access with bounded retries and strict response validation."""

import json
import http.client
import re
import time
import urllib.error
import urllib.request


def published_versions(package_id: str) -> list[str]:
    if re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", package_id) is None:
        raise ValueError("Invalid registry package id")
    url = f"https://api.nuget.org/v3-flatcontainer/{package_id.lower()}/index.json"
    for attempt in range(3):
        try:
            with urllib.request.urlopen(url, timeout=30) as response:
                if response.status != 200:
                    raise ValueError(f"Unexpected NuGet HTTP status: {response.status}")
                data = json.load(response)
            versions = data.get("versions") if isinstance(data, dict) else None
            if not isinstance(versions, list) or not versions or any(not isinstance(v, str) or not v for v in versions):
                raise ValueError(f"Malformed NuGet version index: {package_id}")
            return [version.lower() for version in versions]
        except urllib.error.HTTPError as error:
            if error.code == 404:
                return []
            if error.code < 500 and error.code != 429:
                raise ValueError(f"NuGet index returned HTTP {error.code}: {package_id}") from error
            if attempt == 2:
                raise ValueError(f"NuGet index unavailable: {package_id}") from error
        except (urllib.error.URLError, TimeoutError, http.client.HTTPException, ConnectionError) as error:
            if attempt == 2:
                raise ValueError(f"NuGet index unavailable: {package_id}") from error
        time.sleep(2)
    raise ValueError(f"NuGet index unavailable: {package_id}")
