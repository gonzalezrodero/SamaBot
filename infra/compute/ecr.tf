resource "aws_ecr_repository" "backend" {
  name                 = "${var.project_name}-api"
  image_tag_mutability = "IMMUTABLE" # Security: Prevent tag overwriting

  encryption_configuration {
    encryption_type = "KMS" # Security: Encrypt images at rest using AWS KMS
  }

  image_scanning_configuration {
    scan_on_push = true # Security: Automatically scan for vulnerabilities
  }

  force_delete = false
}

# Lifecycle policy to automatically delete old untagged images and save costs
resource "aws_ecr_lifecycle_policy" "backend_policy" {
  repository = aws_ecr_repository.backend.name

  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Keep last 30 images"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = 30
      }
      action = {
        type = "expire"
      }
    }]
  })
}