const WebSocket = require('ws');
// const iconv = require('iconv-lite'); // 현재 코드에서 사용되지 않아 주석 처리했습니다. 필요시 해제하세요.

class GameServer {

    constructor(port) {
        this.wss = new WebSocket.Server({ port });
        this.clients = new Set();
        this.players = new Map();
        this.SetupServerEvent();
        console.log(`게임 서버 포트 ${port}에서 시작 되었습니다.`);
    }

    SetupServerEvent() {
        this.wss.on('connection', (socket) => {
            this.clients.add(socket);
            const playerId = this.generatePlayerId();

            // 초기 플레이어 데이터 저장 (사진 4 내용 반영)
            this.players.set(playerId, {
                socket: socket,
                position: { x: 0, y: 0, z: 0 },
                rotation: { x: 0, y: 0, z: 0 }
            });

            console.log(`클라이언트 접속! ID : ${playerId}, 현재 접속자 : ${this.clients.size}`);

            const welcomData = {
                type: 'connection',
                playerId: playerId,
                message: '서버에 연결 되었습니다!'
            };

            // 기존 플레이어들 정보를 새 플레이어에게 전송 (사진 2 내용 반영)
            this.players.forEach((player, pid) => {
                if (pid !== playerId) {
                    const joinMsg = {
                        type: 'playerJoin',
                        playerId: pid,
                        position: player.position,
                        rotation: player.rotation
                    };
                    socket.send(JSON.stringify(joinMsg));
                    console.log(`기존 플레이어 정보 전송 : ${pid} -> ${playerId}`);
                }
            });

            socket.send(JSON.stringify(welcomData));

            socket.on('message', (message) => {
                try {
                    const data = JSON.parse(message);
                    
                    // 위치 업데이트 로그는 너무 많아서 제외하고 나머지 타입만 로그 출력 (사진 3 내용 참고)
                    if(data.type !== 'positionUpdate') {
                        console.log('수신된 메세지 :', data);
                    }

                    if (data.type === 'chat') {
                        // 채팅 메세지 브로드캐스트 (사진 1 내용 반영)
                        this.broadcast({
                            type: 'chat',
                            playerId: playerId,
                            message: data.message
                        });
                    }
                    else if (data.type === 'positionUpdate') {
                        // 위치/회전 업데이트 로직 (사진 0 내용 추가됨)
                        const player = this.players.get(playerId);
                        if (player) {
                            if (data.position) {
                                player.position = data.position; // 서버 메모리에 위치 저장
                            }
                            if (data.rotation) {
                                player.rotation = data.rotation; // 서버 메모리에 회전 저장
                            }

                            // 다른 플레이어들에게만 브로드캐스트 (보낸 본인 제외)
                            const updateMsg = {
                                type: 'positionUpdate',
                                playerId: playerId,
                                position: player.position,
                                rotation: player.rotation
                            };
                            // 두 번째 인자로 socket을 넘겨서 이 소켓에는 보내지 않도록 함
                            this.broadcast(updateMsg, socket);
                        }
                    }
                } catch (error) {
                    console.error('메세지 파싱 에러 :', error);
                }
            });

            socket.on('close', () => {
                this.clients.delete(socket);
                this.players.delete(playerId);

                // 나간 플레이어 정보 브로드캐스트
                this.broadcast({
                    type: 'playerDisconnect',
                    playerId: playerId
                });

                console.log(`클라이언트 퇴장 ID : ${playerId}, 현재 접속자 : ${this.clients.size}`);
            });

            socket.on('error', (error) => {
                console.error('소켓 에러 :', error);
            });
        });
    }

    // 특정 소켓 제외 브로드캐스트 기능이 추가된 버전 (사진 4 내용 반영 및 오타 수정)
    broadcast(data, excludeSocket = null) {
        const message = JSON.stringify(data);
        this.clients.forEach(client => {
            // client가 excludeSocket(보낸 사람)이 아니고, 연결이 열려있을 때만 전송
            if (client !== excludeSocket && client.readyState === WebSocket.OPEN) {
                client.send(message);
            }
        });
    }

    generatePlayerId() {
        return 'player_' + Math.random().toString(36).substr(2, 9);
    }
}

const gameServer = new GameServer(3000);