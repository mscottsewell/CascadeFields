# Deployment Checklist

Use this checklist when deploying the CascadeFields plugin to ensure nothing is missed.

## Pre-Deployment

- [ ] **Solution built successfully** in Release mode
- [ ] **Configuration JSON validated** (use jsonlint.com)
- [ ] **Parent entity identified** (what entity triggers the cascade)
- [ ] **Child entities identified** (what entities receive cascaded values)
- [ ] **Field mappings documented**
  - [ ] Source fields exist on parent entity
  - [ ] Target fields exist on child entities
  - [ ] Field types are compatible
- [ ] **Trigger fields identified** (which fields should trigger the cascade)
- [ ] **Relationship or lookup field identified** for each child entity
- [ ] **Filter criteria defined** (if needed)
- [ ] **Test environment available**

## Assembly Registration

- [ ] **Plugin Registration Tool installed** and opened
- [ ] **Connected to target environment**
- [ ] **Assembly registered**:
  - [ ] Isolation Mode: Sandbox
  - [ ] Location: Database
  - [ ] Assembly visible in tree

## Step Registration

- [ ] **Plugin step registered**:
  - [ ] Message: Update
  - [ ] Primary Entity: [Your parent entity]
  - [ ] Stage: PostOperation (40)
  - [ ] Mode: Asynchronous
  - [ ] Unsecure Configuration: [JSON pasted]
  - [ ] Filtering Attributes: [Trigger fields selected]

## Image Registration

- [ ] **PreImage registered**:
  - [ ] Image Type: PreImage
  - [ ] Name: PreImage
  - [ ] Entity Alias: PreImage
  - [ ] Parameters: All Attributes (or specific fields)

## Security & Permissions

- [ ] **Service account permissions verified**:
  - [ ] Read permission on parent entity
  - [ ] Read permission on child entities
  - [ ] Update permission on child entities
- [ ] **User permissions verified**:
  - [ ] Users can update parent entity
  - [ ] Users can update child entities (for manual verification)

## Testing

- [ ] **Test record identified** in parent entity
- [ ] **Related child records verified**:
  - [ ] Child records exist
  - [ ] Child records match filter criteria
  - [ ] Lookup/relationship field is populated
- [ ] **Test execution performed**:
  - [ ] Parent record updated (trigger field changed)
  - [ ] Record saved
  - [ ] Wait 10-30 seconds (async processing)
  - [ ] Child records checked and updated correctly
- [ ] **Plugin trace log reviewed**:
  - [ ] Execution started
  - [ ] Configuration loaded
  - [ ] Trigger fields detected
  - [ ] Child records found
  - [ ] Updates completed
  - [ ] Execution completed successfully
- [ ] **System job verified**:
  - [ ] Job status: Succeeded
  - [ ] No error messages

## Post-Deployment

- [ ] **Documentation updated**:
  - [ ] Configuration saved to repository/documentation
  - [ ] Deployment date recorded
  - [ ] Plugin step ID recorded (for future reference)
- [ ] **Stakeholders notified**:
  - [ ] Users informed of new functionality
  - [ ] Support team aware of plugin
  - [ ] Help desk documentation updated
- [ ] **Monitoring plan established**:
  - [ ] Trace logs will be monitored for [X days/weeks]
  - [ ] System jobs will be checked daily
  - [ ] Users will report any issues

## Troubleshooting Reference

### If plugin doesn't execute:
1. Check filtering attributes are set correctly
2. Verify message and stage are correct (Update, PostOperation)
3. Confirm entity name matches parent entity in config
4. Check plugin is active (not disabled)

### If child records not updated:
1. Verify filter criteria matches child records
2. Check lookup/relationship field is populated correctly
3. Confirm field names are correct (case-sensitive)
4. Verify child records exist and are accessible

### If errors occur:
1. Check plugin trace logs for detailed error messages
2. Verify JSON configuration is valid
3. Confirm all required fields in configuration are present
4. Check depth limit not exceeded (max depth: 2)
5. Verify user has permissions on all entities

## Rollback Plan

If issues arise and rollback is needed:

- [ ] **Disable plugin step** (don't delete immediately)
  - Right-click step > Disable
- [ ] **Monitor for 24 hours** to ensure no dependencies
- [ ] **Document issue** for future resolution
- [ ] **Unregister step** (if permanently removing)
- [ ] **Unregister assembly** (if completely removing plugin)

## Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Developer | | | |
| Tester | | | |
| System Admin | | | |
| Business Owner | | | |

## Notes

_Use this section for environment-specific notes, special considerations, or issues encountered during deployment._

---

**Plugin Version**: 1.0.0  
**Deployment Date**: _______________  
**Environment**: _______________  
**Deployed By**: _______________
