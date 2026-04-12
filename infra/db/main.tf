provider "aws" {
  region = var.aws_region
}

# ==========================================
# SECURITY GROUP
# ==========================================
resource "aws_security_group" "db_sg" {
  name        = "${var.project_name}-db-sg"
  description = "Allow inbound PostgreSQL traffic"
  vpc_id      = data.terraform_remote_state.network.outputs.vpc_id

  ingress {
    description = "PostgreSQL from within VPC"
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    # For now, allow all VPC traffic. 
    # Later we will scope this down to the ECS Tasks Security Group.
    cidr_blocks = ["10.0.0.0/16"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# ==========================================
# DB SUBNET GROUP
# ==========================================
resource "aws_db_subnet_group" "db_subnets" {
  name       = "${var.project_name}-db-subnet-group"
  subnet_ids = data.terraform_remote_state.network.outputs.private_subnet_ids

  tags = {
    Name = "${var.project_name}-db-subnet-group"
  }
}

# ==========================================
# RDS INSTANCE (PostgreSQL)
# ==========================================
resource "aws_db_instance" "postgres" {
  identifier            = "${var.project_name}-db"
  engine                = "postgres"
  engine_version        = "16"
  
  # Usar las variables dinámicas
  instance_class        = var.db_instance_class
  allocated_storage     = var.allocated_storage
  multi_az              = var.multi_az

  max_allocated_storage = 100
  storage_type          = "gp3"

  db_name  = "chatbot"
  username = "dbadmin"

  storage_encrypted       = true
  backup_retention_period = 7

  db_subnet_group_name   = aws_db_subnet_group.db_subnets.name
  vpc_security_group_ids = [aws_security_group.db_sg.id]
  publicly_accessible    = false
  skip_final_snapshot    = true

  manage_master_user_password = true
}