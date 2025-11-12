# Hướng Dẫn Deploy Quản Lý Ăn Trưa lên VPS

Hướng dẫn chi tiết để deploy ứng dụng ASP.NET Core lên VPS Linux sử dụng Docker.

## 📋 Yêu Cầu

- VPS Linux (Ubuntu 20.04+ hoặc tương đương)
- Docker đã được cài đặt
- Docker Compose đã được cài đặt
- Git (để clone/pull code)
- Nginx (khuyến nghị, để làm reverse proxy)

## 🚀 Các Bước Deploy

### 1. Kết Nối VPS và Clone Code

```bash
# SSH vào VPS
ssh user@your-vps-ip

# Tạo thư mục project
mkdir -p ~/projects
cd ~/projects

# Clone repository (hoặc pull nếu đã có)
git clone <your-git-repo-url> QuanLyAnTrua
cd QuanLyAnTrua
```

### 2. Cấu Hình Environment Variables

Tạo file `.env` trong thư mục gốc của project:

```bash
nano .env
```

Nội dung file `.env`:

```env
# Casso Webhook Configuration
CASSO_WEBHOOK_SECRET=your_webhook_secret_here
CASSO_SECURE_TOKEN=your_secure_token_here

# Telegram Bot Configuration
TELEGRAM_BOT_TOKEN=your_telegram_bot_token_here
TELEGRAM_BOT_USERNAME=thongbaoantrua_bot
```

**Lưu ý**: Thay thế các giá trị `your_*_here` bằng giá trị thực tế của bạn.

### 3. Deploy với Script Tự Động

```bash
# Cấp quyền thực thi cho script
chmod +x deploy.sh

# Chạy script deploy
./deploy.sh
```

Script sẽ tự động:
- Tạo các thư mục cần thiết (data, logs, avatars)
- Build Docker image
- Khởi động container

### 4. Deploy Thủ Công (Nếu không dùng script)

```bash
# Tạo các thư mục
mkdir -p data logs avatars

# Build và start
docker-compose up -d --build

# Xem logs
docker-compose logs -f
```

### 5. Cấu Hình Nginx Reverse Proxy (Khuyến Nghị)

Tạo file cấu hình Nginx:

```bash
sudo nano /etc/nginx/sites-available/quanlyantrua
```

Nội dung:

```nginx
server {
    listen 80;
    server_name your-domain.com;  # Thay bằng domain của bạn

    # Redirect HTTP to HTTPS (nếu có SSL)
    # return 301 https://$server_name$request_uri;

    # Hoặc proxy trực tiếp nếu chưa có SSL
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}

# Nếu có SSL (Let's Encrypt)
# server {
#     listen 443 ssl http2;
#     server_name your-domain.com;
# 
#     ssl_certificate /etc/letsencrypt/live/your-domain.com/fullchain.pem;
#     ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;
# 
#     location / {
#         proxy_pass http://localhost:5000;
#         proxy_http_version 1.1;
#         proxy_set_header Upgrade $http_upgrade;
#         proxy_set_header Connection keep-alive;
#         proxy_set_header Host $host;
#         proxy_set_header X-Real-IP $remote_addr;
#         proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
#         proxy_set_header X-Forwarded-Proto $scheme;
#         proxy_cache_bypass $http_upgrade;
#     }
# }
```

Kích hoạt site:

```bash
sudo ln -s /etc/nginx/sites-available/quanlyantrua /etc/nginx/sites-enabled/
sudo nginx -t  # Kiểm tra cấu hình
sudo systemctl reload nginx
```

### 6. Cấu Hình Firewall

```bash
# Mở port 80 và 443 (cho nginx)
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Nếu không dùng nginx, mở port 5000
# sudo ufw allow 5000/tcp

# Kiểm tra firewall status
sudo ufw status
```

### 7. Cấu Hình SSL với Let's Encrypt (Tùy Chọn)

```bash
# Cài đặt Certbot
sudo apt update
sudo apt install certbot python3-certbot-nginx

# Lấy SSL certificate
sudo certbot --nginx -d your-domain.com

# Certbot sẽ tự động cấu hình nginx và renew certificate
```

## 🔄 Cập Nhật Ứng Dụng

Khi có code mới, cập nhật như sau:

```bash
# Pull code mới
git pull

# Rebuild và restart
docker-compose up -d --build

# Hoặc chỉ restart nếu không có thay đổi code
docker-compose restart
```

## 📊 Quản Lý Container

### Xem Logs

```bash
# Xem logs real-time
docker-compose logs -f

# Xem logs của service cụ thể
docker-compose logs -f quanlyantrua

# Xem logs với số dòng giới hạn
docker-compose logs --tail=100 -f
```

### Dừng/Start/Restart

```bash
# Dừng containers
docker-compose down

# Start containers
docker-compose up -d

# Restart containers
docker-compose restart

# Xem status
docker-compose ps
```

### Backup Database

```bash
# Backup database SQLite
cp data/QuanLyAnTrua.db data/QuanLyAnTrua.db.backup.$(date +%Y%m%d_%H%M%S)
```

### Restore Database

```bash
# Restore từ backup
cp data/QuanLyAnTrua.db.backup.YYYYMMDD_HHMMSS data/QuanLyAnTrua.db
docker-compose restart
```

## 🗂️ Cấu Trúc Thư Mục trên VPS

```
~/projects/QuanLyAnTrua/
├── data/              # Database SQLite (persistent)
├── logs/              # Log files (persistent)
├── avatars/           # Avatar uploads (persistent)
├── docker-compose.yml
├── Dockerfile
├── .env               # Environment variables (KHÔNG commit lên git)
└── ... (source code)
```

## 🔒 Bảo Mật

1. **Không commit file `.env`**: File này chứa secrets, đã được thêm vào `.gitignore`
2. **Sử dụng HTTPS**: Cấu hình SSL với Let's Encrypt
3. **Firewall**: Chỉ mở các port cần thiết
4. **Regular Updates**: Cập nhật Docker images và hệ điều hành thường xuyên

## 🐛 Troubleshooting

### Container không start

```bash
# Xem logs để tìm lỗi
docker-compose logs

# Kiểm tra port đã bị sử dụng chưa
sudo netstat -tulpn | grep 5000
```

### Database migration lỗi

```bash
# Vào trong container và chạy migration thủ công
docker-compose exec quanlyantrua dotnet ef database update
```

### Permission denied

```bash
# Đảm bảo thư mục có quyền ghi
sudo chown -R $USER:$USER data logs avatars
chmod -R 755 data logs avatars
```

### Ứng dụng không truy cập được từ bên ngoài

1. Kiểm tra firewall: `sudo ufw status`
2. Kiểm tra nginx: `sudo nginx -t && sudo systemctl status nginx`
3. Kiểm tra container: `docker-compose ps`
4. Kiểm tra logs: `docker-compose logs`

## 📝 Ghi Chú

- Database SQLite được lưu trong thư mục `data/` để đảm bảo persistence
- Logs được lưu trong thư mục `logs/` với rolling interval theo ngày
- Avatar uploads được lưu trong thư mục `avatars/`
- Tất cả dữ liệu quan trọng đều được mount vào volumes để không bị mất khi container restart

## 🆘 Hỗ Trợ

Nếu gặp vấn đề, kiểm tra:
1. Logs của container: `docker-compose logs -f`
2. Logs của nginx: `sudo tail -f /var/log/nginx/error.log`
3. System logs: `journalctl -u docker`

