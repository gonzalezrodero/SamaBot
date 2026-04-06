provider "aws" {
  region = var.aws_region
}

terraform {
  backend "s3" {
    bucket         = "chatbot-tf-state-543704476214"
    key            = "bootstrap/terraform.tfstate"
    region         = "eu-west-1"
    dynamodb_table = "chatbot-tf-lock"
    encrypt        = true
  }
}

# --- State Storage (S3) ---
resource "aws_s3_bucket" "tf_state" {
  bucket = "${var.project_name}-tf-state-${var.aws_account_id}"
}

# --- State Locking (DynamoDB) ---
resource "aws_dynamodb_table" "tf_lock" {
  name         = "${var.project_name}-tf-lock"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "LockID"

  attribute {
    name = "LockID"
    type = "S"
  }
}

# --- GitHub OIDC Identity Provider ---
resource "aws_iam_openid_connect_provider" "github" {
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = ["6938fd4d98bab03faadb97b34396831e3780aea1"] # GitHub's SHA1 thumbprint
}

# --- IAM Role for GitHub Actions ---
resource "aws_iam_role" "github_actions" {
  name = "${var.project_name}-github-deploy-role"

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
          StringLike = {
            "token.actions.githubusercontent.com:sub" : "repo:${var.github_owner}/${var.github_repo}:*"
          },
          StringEquals = {
            "token.actions.githubusercontent.com:aud" : "sts.amazonaws.com"
          }
        }
      }
    ]
  })
}

# Permission: Initial Admin Access (To be scoped down later)
resource "aws_iam_role_policy_attachment" "admin_attach" {
  role       = aws_iam_role.github_actions.name
  policy_arn = "arn:aws:iam::aws:policy/AdministratorAccess"
}