#!/bin/bash

# Script deploy tự động cho Quản Lý Ăn Trưa
# Sử dụng: ./deploy.sh

set -e  # Exit on error

echo "=========================================="
echo "  Deploy Quản Lý Ăn Trưa lên VPS"
echo "=========================================="
echo ""

# Màu sắc cho output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Kiểm tra Docker
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Docker chưa được cài đặt!${NC}"
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo -e "${RED}Docker Compose chưa được cài đặt!${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Docker và Docker Compose đã được cài đặt${NC}"
echo ""

# Tạo các thư mục cần thiết
echo "Đang tạo các thư mục cần thiết..."
mkdir -p data
mkdir -p logs
mkdir -p avatars
echo -e "${GREEN}✓ Đã tạo các thư mục${NC}"
echo ""

# Kiểm tra file .env
if [ ! -f .env ]; then
    echo -e "${YELLOW}⚠ File .env chưa tồn tại. Đang tạo file .env mẫu...${NC}"
    cat > .env << EOF
# Casso Webhook Configuration
CASSO_WEBHOOK_SECRET=your_webhook_secret_here
CASSO_SECURE_TOKEN=your_secure_token_here

# Telegram Bot Configuration
TELEGRAM_BOT_TOKEN=your_telegram_bot_token_here
TELEGRAM_BOT_USERNAME=thongbaoantrua_bot
EOF
    echo -e "${YELLOW}⚠ Vui lòng chỉnh sửa file .env với các giá trị thực tế!${NC}"
    echo ""
fi

# Pull latest code (nếu đang dùng git)
if [ -d .git ]; then
    echo "Đang pull code mới nhất từ Git..."
    git pull || echo -e "${YELLOW}⚠ Không thể pull từ Git (có thể không có remote hoặc chưa commit)${NC}"
    echo ""
fi

# Build và start containers
echo "Đang build Docker image..."
docker-compose build --no-cache

echo ""
echo "Đang khởi động containers..."
docker-compose up -d

echo ""
echo -e "${GREEN}=========================================="
echo "  Deploy thành công!"
echo "==========================================${NC}"
echo ""
echo "Ứng dụng đang chạy tại: http://localhost:5000"
echo ""
echo "Các lệnh hữu ích:"
echo "  - Xem logs: docker-compose logs -f"
echo "  - Dừng app: docker-compose down"
echo "  - Restart app: docker-compose restart"
echo "  - Xem status: docker-compose ps"
echo ""
echo -e "${YELLOW}Lưu ý: Nếu deploy lên VPS, bạn cần:${NC}"
echo "  1. Cấu hình reverse proxy (nginx) để trỏ về port 5000"
echo "  2. Cấu hình SSL/HTTPS"
echo "  3. Mở port 5000 trong firewall (hoặc chỉ mở 80/443 cho nginx)"
echo ""

