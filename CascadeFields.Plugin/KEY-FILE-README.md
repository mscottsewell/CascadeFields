# Strong Name Key File (CascadeFields.snk)

## ⚠️ IMPORTANT - Key File Security

This file (`CascadeFields.snk`) contains the strong name key pair used to sign the `CascadeFields.Plugin` assembly.

### Security Considerations

**DO NOT:**
- ❌ Commit this file to source control (it's in .gitignore)
- ❌ Share this file publicly
- ❌ Email or transmit this file insecurely
- ❌ Store this file in cloud storage without encryption

**DO:**
- ✅ Keep this file secure and backed up in a safe location
- ✅ Use the same key file across all environments for consistency
- ✅ Store in a secure password manager or encrypted storage
- ✅ Regenerate if the key is ever compromised

### Why Strong Naming?

Microsoft Dataverse **requires** all plugin assemblies to be strongly signed for security reasons:
- Ensures assembly integrity
- Prevents assembly tampering
- Guarantees unique assembly identity
- Enables side-by-side versioning

### Regenerating the Key File

If you need to regenerate the key file (e.g., for a new installation or if compromised):

```powershell
# Using Windows SDK Strong Name tool
sn -k CascadeFields.Plugin\CascadeFields.snk

# Or find sn.exe automatically
$snExePath = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Microsoft SDKs\Windows" -Recurse -Filter "sn.exe" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
& $snExePath -k "CascadeFields.Plugin\CascadeFields.snk"
```

### Verifying the Assembly is Signed

After building, verify the assembly is properly signed:

```powershell
# Verify strong name signature
sn -vf "CascadeFields.Plugin\bin\Release\net462\CascadeFields.Plugin.dll"

# Expected output: "Assembly 'CascadeFields.Plugin.dll' is valid"
```

### Using in CI/CD Pipelines

For automated builds:

1. **Store Securely**: Store the .snk file as a secure file or secret in your CI/CD system
2. **Download at Build**: Download the key file to the build agent
3. **Build**: Build the project with the key file present
4. **Clean Up**: Delete the key file from the agent after build

Example for Azure DevOps:

```yaml
steps:
- task: DownloadSecureFile@1
  name: snkFile
  inputs:
    secureFile: 'CascadeFields.snk'
    
- script: |
    copy $(snkFile.secureFilePath) $(Build.SourcesDirectory)\CascadeFields.Plugin\CascadeFields.snk
    dotnet build -c Release
  displayName: 'Build Plugin'
```

### Key File Location

The key file must be located at:
```
CascadeFields.Plugin\CascadeFields.snk
```

This path is referenced in `CascadeFields.Plugin.csproj`:

```xml
<PropertyGroup>
  <SignAssembly>true</SignAssembly>
  <AssemblyOriginatorKeyFile>CascadeFields.snk</AssemblyOriginatorKeyFile>
</PropertyGroup>
```

### Troubleshooting

**Error: "Error signing output with public key from file 'CascadeFields.snk' -- Invalid public key"**

Solution: The key file is corrupted or invalid. Regenerate it using the sn.exe tool.

**Error: "Could not find file 'CascadeFields.snk'"**

Solution: Ensure the key file is in the correct location (`CascadeFields.Plugin\CascadeFields.snk`).

### Build Verification

After building with strong name signing, you should see:

```
Build succeeded in X.Xs
  CascadeFields.Plugin net462 succeeded (X.Xs) → CascadeFields.Plugin\bin\Release\net462\CascadeFields.Plugin.dll
```

And verification shows:

```powershell
PS> sn -vf CascadeFields.Plugin\bin\Release\net462\CascadeFields.Plugin.dll

Microsoft (R) .NET Framework Strong Name Utility  Version 4.0.30319.0
Copyright (c) Microsoft Corporation.  All rights reserved.

Assembly 'CascadeFields.Plugin.dll' is valid
```

## Summary

- ✅ Assembly is strongly signed
- ✅ Required for Dataverse plugin deployment
- ✅ Key file excluded from source control
- ✅ Secure the key file appropriately
- ✅ Use same key across all builds for consistency
