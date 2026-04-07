# ==========================================
# 1. GITHUB OIDC IDENTITY PROVIDER
# ==========================================
# Establishes a trust relationship between AWS and GitHub Actions.
# This allows workflows to obtain short-lived credentials securely 
# without storing static AWS Access Keys in GitHub Secrets.
resource "aws_iam_openid_connect_provider" "github" {
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = ["6938fd4d98bab03faadb97b34396831e3780aea1"] # Official GitHub Actions SHA1 thumbprint
}

# ==========================================
# 2. IAM ROLE FOR GITHUB ACTIONS
# ==========================================
# The role that GitHub workflows will assume when deploying infrastructure.
resource "aws_iam_role" "github_actions" {
  name = "${var.project_name}-github-deploy-role"

  # Trust Policy: Dictates EXACTLY who is allowed to assume this role.
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRoleWithWebIdentity"
        Effect = "Allow"
        Principal = {
          Federated = aws_iam_openid_connect_provider.github.arn
        }
        Condition = {
          # CRITICAL SECURITY CONTROL: 
          # Restrict access exclusively to your specific repository.
          # Without this line, ANY GitHub repository in the world could assume your role.
          StringLike = {
            "token.actions.githubusercontent.com:sub" : "repo:${var.github_owner}/${var.github_repo}:*"
          },
          # Ensure the audience is always the AWS STS service
          StringEquals = {
            "token.actions.githubusercontent.com:aud" : "sts.amazonaws.com"
          }
        }
      }
    ]
  })
}

# ==========================================
# 3. ROLE PERMISSIONS
# ==========================================
# Grants Administrator access to the GitHub Actions role.
# TODO: Once the core infrastructure is stable, scope this down to follow 
# the Principle of Least Privilege (e.g., specific VPC, S3, and RDS permissions).
resource "aws_iam_role_policy_attachment" "admin_attach" {
  role       = aws_iam_role.github_actions.name
  policy_arn = "arn:aws:iam::aws:policy/AdministratorAccess"
}