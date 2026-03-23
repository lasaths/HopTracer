# HopTracer — Privacy Policy

**Last updated: March 23, 2026**

---

## Overview

HopTracer is a local Windows desktop application for comparing Grasshopper definition files (`.gh` / `.ghx`). All file processing happens entirely on your device. HopTracer does not collect personal information, does not track usage, and does not send your files or project data anywhere.

---

## Data We Do Not Collect

HopTracer does **not** collect, transmit, or store:

- Personal identity information (name, email, account credentials)
- Usage analytics or telemetry
- Crash reports sent to external services
- The Grasshopper files you open or compare
- File paths, project names, or folder structures
- Device identifiers or machine fingerprints
- Location, IP address, or network information
- Clipboard contents or screen activity

There are no third-party analytics, advertising, or tracking SDKs in HopTracer.

---

## Data Stored Locally on Your Device

HopTracer stores the following data **only on your machine**, in `%LOCALAPPDATA%\HopTracer\`:

| Data | Purpose | Retention |
|------|---------|-----------|
| Uploaded `.gh`/`.ghx` files | Temporary processing cache | Auto-deleted after 24 hours |
| Baseline comparison metadata | Saved explicitly by you for review workflows | Until you delete them |
| Update notification preference | Remembers the last update version you were notified about | Local device only |

None of this data is transmitted anywhere.

---

## Network Access

HopTracer makes **one** external network request:

- **Endpoint:** `https://api.github.com/repos/lasaths/HopTracer/releases/latest`
- **Purpose:** Check whether a newer version of HopTracer is available
- **Frequency:** Once on startup, cached for 6 hours
- **Data sent:** Standard HTTP headers only — no user data, no file content, no identifiers
- **Your control:** Update notifications can be dismissed indefinitely; no update is ever installed automatically

GitHub's privacy policy applies to this request: https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement

---

## Your Grasshopper Files

Files you open in HopTracer are processed entirely on your device. They are never uploaded to a cloud service, never transmitted to any server, and never shared with third parties. Temporary copies written during processing are automatically deleted within 24 hours.

---

## Children's Privacy

HopTracer is a professional tool not directed at children under 13. No data is collected from any user, including children.

---

## Third-Party Components

HopTracer uses only Microsoft-published libraries (.NET, .NET MAUI, ASP.NET Core). No third-party analytics platforms, advertising networks, or tracking SDKs are included.

The optional `.gh` binary parser loads `GH_IO.dll` from your local Rhino installation if present. This is a read-only local operation; no data is sent to Robert McNeel & Associates.

---

## Microsoft Store

If you installed HopTracer via the Microsoft Store, Microsoft's own privacy practices apply to the Store platform. See: https://privacy.microsoft.com/privacystatement

---

## Changes to This Policy

If this policy changes in a meaningful way, the "Last updated" date above will be updated and the new policy published with the next release.

---

## Contact

For privacy questions, open an issue at:  
https://github.com/lasaths/HopTracer/issues

---

*This privacy policy was drafted with the assistance of AI (GitHub Copilot).*
