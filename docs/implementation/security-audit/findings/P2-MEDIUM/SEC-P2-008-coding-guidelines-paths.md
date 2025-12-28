# SEC-P2-008: External Path References in CODING_GUIDELINES

## 1. Metadata

| Field | Value |
|-------|-------|
| **Finding ID** | SEC-P2-008 |
| **Title** | CODING_GUIDELINES.md References Files Outside Repository |
| **Priority** | P2 - MEDIUM |
| **Severity** | Medium |
| **Category** | Documentation |
| **Status** | Not Started |
| **Effort Estimate** | 2 hours |
| **Implementation Phase** | Phase 3 (Week 3, Day 7) |
| **Assigned To** | Technical Writer |
| **Created** | 2025-12-27 |

---

## 2. Cross-References

| Type | Reference |
|------|-----------|
| **Audit Report** | `/home/tshepo/projects/tw/COMPREHENSIVE_AUDIT_REPORT.md:531-538` |
| **Code Files** | `/home/tshepo/projects/tw/docs/development/CODING_GUIDELINES.md` |
| **Dependencies** | SEC-P2-002 (GETTING_STARTED missing), SEC-P2-001 (Root README) |
| **Compliance Impact** | None (Portability issue) |

---

## 3. Executive Summary

### Problem

CODING_GUIDELINES.md references files outside the repository using absolute paths:
- `/home/tshepo/projects/GBMM/docs/planning/100_PERCENT_ROADMAP.md`
- `/home/tshepo/projects/GBMM/docs/development/DEVELOPMENT_WORKFLOW.md`
- `/home/tshepo/projects/GBMM/gbmm/auth-service/` (example implementations)

### Impact

- **Broken Links:** Paths don't exist on other machines or CI/CD
- **Poor Portability:** Documentation only works on one developer's machine
- **CI/CD Failures:** Automated documentation builds fail

### Solution

Move referenced files into `tw` repository or replace with repository-relative paths.

---

## 4. Implementation Guide

### Step 1: Identify All External References (15 minutes)

```bash
cd docs/development

# Find all absolute path references
grep -n "/home/tshepo/projects/GBMM" CODING_GUIDELINES.md

# Output:
# 23:/home/tshepo/projects/GBMM/docs/planning/100_PERCENT_ROADMAP.md
# 45:/home/tshepo/projects/GBMM/docs/development/DEVELOPMENT_WORKFLOW.md
# 67:/home/tshepo/projects/GBMM/gbmm/auth-service/
```

### Step 2: Create Missing Files in Repository (1 hour)

**Create docs/planning/100_PERCENT_ROADMAP.md:**
```bash
mkdir -p docs/planning

cat > docs/planning/100_PERCENT_ROADMAP.md <<'EOF'
# 100% Implementation Roadmap

This document tracks the comprehensive implementation roadmap for TW platform.

## Current Status

See [COMPREHENSIVE_AUDIT_REPORT.md](/COMPREHENSIVE_AUDIT_REPORT.md) for current implementation gaps.

## Implementation Phases

See [docs/implementation/security-audit/ROADMAP.md](../implementation/security-audit/ROADMAP.md) for the 6-phase implementation plan.

## Feature Completion Tracking

| Feature | Spec | Implementation | Tests | Docs | Status |
|---------|------|----------------|-------|------|--------|
| USP Auth | ✅ | ✅ | ✅ | ✅ | Complete |
| USP Secrets | ✅ | ✅ | ✅ | ✅ | Complete |
| USP Vault | ✅ | ✅ | ✅ | ⚠️ | 90% Complete |
| UCCP | ✅ | 🚧 | ❌ | ❌ | In Progress |
| NCCS | ✅ | 🚧 | ❌ | ❌ | In Progress |
| UDPS | ✅ | 🚧 | ❌ | ❌ | In Progress |
| Stream Compute | ✅ | 🚧 | ❌ | ❌ | In Progress |

EOF
```

**Create docs/development/DEVELOPMENT_WORKFLOW.md:**
```bash
cat > docs/development/DEVELOPMENT_WORKFLOW.md <<'EOF'
# Development Workflow

This document describes the development workflow for TW platform.

## Branch Strategy

- **main:** Production-ready code
- **develop:** Integration branch for features
- **feature/*:** Feature development branches
- **hotfix/*:** Emergency production fixes

## Development Cycle

1. Create feature branch from develop
2. Implement feature following CODING_GUIDELINES.md
3. Write tests (unit, integration)
4. Update documentation
5. Create pull request
6. Code review (2 approvals required)
7. Merge to develop

## Pull Request Requirements

- [ ] All tests pass
- [ ] Code coverage > 80%
- [ ] No security vulnerabilities (Snyk/Trivy scan)
- [ ] Documentation updated
- [ ] Changelog updated
- [ ] 2 code review approvals

## Code Review Checklist

- [ ] Code follows CODING_GUIDELINES.md
- [ ] Tests cover new functionality
- [ ] No hardcoded secrets
- [ ] Error handling appropriate
- [ ] Logging structured and informative
- [ ] Performance considerations addressed

## Release Process

1. Create release branch from develop
2. Update version numbers
3. Update CHANGELOG.md
4. Integration testing
5. Merge to main
6. Tag release
7. Deploy to production
8. Merge back to develop

EOF
```

### Step 3: Update CODING_GUIDELINES.md References (30 minutes)

```bash
# Update all absolute paths to repository-relative paths
cd docs/development

# Backup original
cp CODING_GUIDELINES.md CODING_GUIDELINES.md.bak

# Replace absolute paths with relative paths
sed -i 's|/home/tshepo/projects/GBMM/docs/planning/100_PERCENT_ROADMAP.md|../planning/100_PERCENT_ROADMAP.md|g' CODING_GUIDELINES.md
sed -i 's|/home/tshepo/projects/GBMM/docs/development/DEVELOPMENT_WORKFLOW.md|DEVELOPMENT_WORKFLOW.md|g' CODING_GUIDELINES.md

# For auth-service examples, reference USP instead (already in repo)
sed -i 's|/home/tshepo/projects/GBMM/gbmm/auth-service/|../../services/usp/src/USP.API/Controllers/v1/AuthController.cs|g' CODING_GUIDELINES.md
```

### Step 4: Verify All Links (15 minutes)

```bash
# Test all markdown links
cd docs/development

# Extract all relative links
grep -o '\[.*\](\.\.\/.*\.md)' CODING_GUIDELINES.md | \
  sed 's/.*(\(.*\))/\1/' | \
  while read link; do
    if [ ! -f "$link" ]; then
      echo "⚠️ Broken link: $link"
    else
      echo "✅ Valid link: $link"
    fi
  done
```

---

## 5. Testing

- [ ] No absolute paths in CODING_GUIDELINES.md
- [ ] All referenced files exist in repository
- [ ] All links verified working
- [ ] Documentation builds successfully in CI/CD
- [ ] Works on multiple developer machines

---

## 6. Compliance Evidence

None (Portability improvement)

---

## 7. Sign-Off

- [ ] **Technical Writer:** All paths updated to repository-relative
- [ ] **Engineering Lead:** Links verified

---

**Finding Status:** Not Started
**Last Updated:** 2025-12-27

---

**End of SEC-P2-008**
