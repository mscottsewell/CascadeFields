<img width="1248" alt="image" src="https://github.com/mscottsewell/CascadeFields/blob/d631690844bc6b50cefff11053299f8594a7c8ee/images/GitHub%20Banner.png" />

# CascadeFields for Microsoft Dataverse

CascadeFields is a Dataverse plug-in plus an XrmToolBox Configurator that helps keep related records in sync by cascading field values from parent records to their related child records.
<img width="1349" height="735" alt="image" src="https://github.com/user-attachments/assets/6c0cd72e-80f4-4b9d-833d-629f343cbd55" />

## Why

Keeping related records consistent in Dataverse often means manual updates, complex Power Automate flows, or custom plug-in development. CascadeFields is intended to give admins and teams a fast, repeatable way to keep child records aligned with parent data while remaining solution-aware and easy to manage.

## What it does

- Cascades changes from a **parent** record to matching **child** records (typically async for performance)
- Populates mapped fields when a **child** record is created and linked to a parent
- Updates mapped fields when a **child** record is re-linked to a different parent
- Supports trigger fields and optional filters to control when and what is updated

## Components

- **CascadeFields Configurator**: XrmToolBox UI used to build, validate, and publish configurations
- **CascadeFields Plugin**: Dataverse plug-in that executes the configured cascade behavior

## Key features

- No-code configuration from within XrmToolBox
- Trigger fields (control which parent changes cause cascades)
- Optional filter criteria (control which child rows are updated)
- Supports parent update cascades and child create/relink scenarios
- Designed for performance (asynchronous parent updates where appropriate)
- Diagnostics via Dataverse plug-in trace logs (when enabled at the environment level)

## Docs Index

- Admin usage (XrmToolBox): [QUICKSTART.md](QUICKSTART.md)
- Configuration JSON reference: [CONFIGURATION.md](CONFIGURATION.md)
- Build, packaging, manual registration, contributing: [PRODEV.md](PRODEV.md)
- Examples: [Examples](Examples/)

## License

Licensed under the [MIT License](LICENSE).
