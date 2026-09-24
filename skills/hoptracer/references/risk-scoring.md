# Risk Assessment Methodology

Detailed explanation of how the hoptracer tool assesses change risks.

## Risk Score Calculation

The tool uses a multi-factor risk scoring system that evaluates different types of changes and their potential impact.

### Score Range

- **0-29**: Low Risk
- **30-59**: Medium Risk
- **60-79**: High Risk
- **80-100**: Critical Risk

## Risk Factors

### Node Status Changes

| Status | Base Score | Example |
|--------|------------|---------|
| Removed | 85 | Component deleted |
| Added | 65 | New component added |
| Modified | 35 | Component properties changed |

### Content Changes

| Change Type | Score | Description |
|-------------|-------|-------------|
| Script/code change | 40 | Python, C#, or expression scripts modified |
| Cluster internals changed | 40 | Cluster content or hash changed |
| Component moved | 10 | Significant position change (>50 units) |

### Connection Changes

| Change Type | Score | Description |
|-------------|-------|-------------|
| Connection changes | 15 | Wirings added/removed/modified |

### Risk Thresholds

The final risk score is clamped to a maximum of 100 and accumulates multiple risk factors.

### Minimum Score for Modified Nodes

If a node is modified but has no detectable risk factors, it receives a minimum score of 15.

## Risk Level Classification

### Critical Risk (≥80)

**Indicators**: Component removal, major structural changes

**Examples**:
- Deleting key components
- Removing entire clusters
- Breaking critical connections
- Removing essential components

**Actions**:
- Requires careful review
- May impact downstream processes
- Consider rollback or mitigation

### High Risk (60-79)

**Indicators**: New components, significant modifications

**Examples**:
- New cluster addition
- New script components
- Significant property changes
- Major cluster internal changes

**Actions**:
- Review changes thoroughly
- Update documentation
- Test affected processes
- Notify stakeholders

### Medium Risk (30-59)

**Indicators**: Script/code changes, cluster modifications

**Examples**:
- Python/C# script modifications
- Expression changes
- Cluster hash changes
- Internal cluster content changes

**Actions**:
- Review logic changes
- Update test cases if needed
- Documentation update recommended
- Consider impact on outputs

### Low Risk (1-29)

**Indicators**: Minor property changes, movements

**Examples**:
- Component position changes
- Parameter adjustments
- Cosmetic changes
- Minor property modifications

**Actions**:
- May not require action
- Consider version control for tracking
- Update if it affects other components

## Risk Score Computation Example

### Example 1: Script Modification

```
Node Status: Modified (35)
+ Script Change: 40
= Total: 75 (High Risk)
```

### Example 2: Cluster Addition

```
Node Status: Added (65)
+ No other changes
= Total: 65 (High Risk)
```

### Example 3: Component Movement

```
Node Status: Modified (35)
+ Movement (10)
+ Connection Changes (15)
= Total: 60 (High Risk) 
```

### Example 4: Minor Property Change

```
Node Status: Modified (35)
+ No content changes
+ No movement
+ No connection changes
= Total: 15 (Low Risk) [Minimum score for modified nodes]
```

## Risk Mitigation Strategies

### For Critical Changes

1. **Immediate Review**: Thoroughly examine all changes
2. **Rollback Consideration**: Keep backup of previous version
3. **Stakeholder Notification**: Inform all affected parties
4. **Impact Analysis**: Test downstream systems
5. **Documentation**: Update all relevant documentation

### For High Changes

1. **Code Review**: Peer review of changes
2. **Testing**: Comprehensive testing of affected areas
3. **Documentation**: Update technical documentation
4. **Monitoring**: Increased monitoring after deployment
5. **Version Control**: Proper git commit practices

### For Medium Changes

1. **Review**: Quick review of logic changes
2. **Test**: Basic testing of modified areas
3. **Document**: Update if functionality changed
4. **Version**: Use descriptive commit messages

### For Low Changes

1. **Track**: Use version control for tracking
2. **Review**: Optional quick review
3. **Document**: Update if user-visible

## Risk Analysis in Different Contexts

### Development Environment

- All change types are acceptable
- Focus on understanding changes
- Use risk scores as learning indicators

### Staging Environment

- Critical changes require approval
- High changes need thorough testing
- Medium changes need basic review
- Low changes are acceptable

### Production Environment

- Critical changes require emergency procedures
- High changes need scheduled deployment
- Medium changes need standard testing
- Low changes acceptable with monitoring

## Risk-Based Workflows

### Pre-commit Check

```bash
hoptracer git current.gh --fail-on-risk
```

- Exits with 1 if any node has score ≥60
- Prevents risky changes from being committed
- Use in git pre-commit hooks

### Continuous Integration

```yaml
- name: Quality Gate
  run: |
    hoptracer compare baseline.gh current.gh --format json -o diff.json
    # Check for critical/high risks
```

- Generate JSON for automated analysis
- Fail pipeline if critical risks detected
- Store results for audit trail

### Release Process

```bash
# Generate comprehensive report
hoptracer compare v1.gh v2.gh \
  --format html \
  --verbose \
  --show-edges \
  -o release-report.html
```

- Full analysis before release
- Document all changes
- Stakeholder review of high-risk items

## Risk Score Limitations

### What the Tool Cannot Detect

- Business impact of changes
- User experience implications
- Performance characteristics
- Security implications
- Dependencies on external systems

### Human Review Essential

Risk scores are indicators, not absolute measures. Always combine automated risk assessment with:

- Domain knowledge
- Context awareness
- Business requirements
- User impact analysis
- System understanding

### False Positives/Negatives

- **False Positives**: Some changes may score high but be benign
- **False Negatives**: Some changes may score low but have impact
- Always review changes in context of their usage

## Monitoring Risk Trends

Track risk scores over time to identify patterns:

```bash
# History analysis
hoptracer git current.gh --commit HEAD~1 --format json -o HEAD-1.json
hoptracer git current.gh --commit HEAD~2 --format json -o HEAD-2.json
hoptracer git current.gh --commit HEAD~3 --format json -o HEAD-3.json
```

Analyze trends in:
- Frequency of high-risk changes
- Types of components that frequently change
- patterns in modification areas
- Risk reduction through better practices
