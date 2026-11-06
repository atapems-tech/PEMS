        (function () {
            var canvas = document.getElementById('signaturePad');
            var ctx = canvas.getContext('2d');

            // چون CSS عرض کانواس رو به‌صورت درصدی تنظیم کرده، باید رزولوشن واقعی پیکسلی رو
            // با اندازه‌ی نمایشی هماهنگ کنیم تا امضا واضح و بدون کش‌ورفتگی ذخیره بشه
            function resizeCanvas() {
                var rect = canvas.getBoundingClientRect();
                canvas.width = rect.width;
                canvas.height = rect.height;
                ctx.fillStyle = '#fff';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
                ctx.lineWidth = 2;
                ctx.lineCap = 'round';
                ctx.strokeStyle = '#1a1a1a';
            }
            resizeCanvas();

            var drawing = false;
            var lastX = 0, lastY = 0;

            function getPos(e) {
                var rect = canvas.getBoundingClientRect();
                if (e.touches && e.touches.length > 0) {
                    return { x: e.touches[0].clientX - rect.left, y: e.touches[0].clientY - rect.top };
                }
                return { x: e.clientX - rect.left, y: e.clientY - rect.top };
            }

            function start(e) {
                drawing = true;
                var pos = getPos(e);
                lastX = pos.x; lastY = pos.y;
                e.preventDefault();
            }

            function move(e) {
                if (!drawing) return;
                var pos = getPos(e);
                ctx.beginPath();
                ctx.moveTo(lastX, lastY);
                ctx.lineTo(pos.x, pos.y);
                ctx.stroke();
                lastX = pos.x; lastY = pos.y;
                e.preventDefault();
            }

            function end() { drawing = false; }

            canvas.addEventListener('mousedown', start);
            canvas.addEventListener('mousemove', move);
            window.addEventListener('mouseup', end);

            canvas.addEventListener('touchstart', start);
            canvas.addEventListener('touchmove', move);
            canvas.addEventListener('touchend', end);

            document.getElementById('clearSignatureBtn').addEventListener('click', function () {
                ctx.fillStyle = '#fff';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
            });

            document.getElementById('saveSignatureBtn').addEventListener('click', function () {
                document.getElementById('signatureDataUrl').value = canvas.toDataURL('image/png');
                document.getElementById('signatureForm').submit();
            });
        })();
