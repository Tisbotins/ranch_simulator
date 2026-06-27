const net = require("net");

const port = Number.parseInt(process.env.PORT || "7778", 10);
const rooms = new Map();

function send(socket, line) {
  if (!socket.destroyed) {
    socket.write(`${line}\n`);
  }
}

function getRoom(code) {
  let room = rooms.get(code);
  if (!room) {
    room = { host: null, guest: null };
    rooms.set(code, room);
  }

  return room;
}

function cleanup(socket) {
  if (!socket.roomCode || !socket.role) {
    return;
  }

  const room = rooms.get(socket.roomCode);
  if (!room) {
    return;
  }

  if (room[socket.role] === socket) {
    room[socket.role] = null;
  }

  const peer = socket.role === "host" ? room.guest : room.host;
  if (peer && !peer.destroyed) {
    send(peer, "__ranch_relay|waiting");
  }

  if (!room.host && !room.guest) {
    rooms.delete(socket.roomCode);
  }
}

function pairIfReady(room) {
  if (!room.host || !room.guest) {
    return;
  }

  send(room.host, "__ranch_relay|paired");
  send(room.guest, "__ranch_relay|paired");
}

function parseHandshake(line) {
  const parts = line.trim().split("|");
  if (
    parts.length !== 3 ||
    parts[0] !== "__ranch_relay" ||
    (parts[1] !== "host" && parts[1] !== "guest") ||
    parts[2].length === 0
  ) {
    return null;
  }

  return {
    role: parts[1],
    roomCode: parts[2].toUpperCase(),
  };
}

const server = net.createServer((socket) => {
  socket.setNoDelay(true);
  socket.setEncoding("utf8");

  let buffer = "";
  let registered = false;

  socket.on("data", (chunk) => {
    buffer += chunk;

    let newline = buffer.indexOf("\n");
    while (newline >= 0) {
      const line = buffer.slice(0, newline).replace(/\r$/, "");
      buffer = buffer.slice(newline + 1);

      if (!registered) {
        const handshake = parseHandshake(line);
        if (!handshake) {
          socket.destroy();
          return;
        }

        const room = getRoom(handshake.roomCode);
        if (room[handshake.role] && !room[handshake.role].destroyed) {
          send(socket, "__ranch_relay|room_full");
          socket.destroy();
          return;
        }

        socket.role = handshake.role;
        socket.roomCode = handshake.roomCode;
        room[handshake.role] = socket;
        registered = true;
        send(socket, "__ranch_relay|waiting");
        pairIfReady(room);
      } else {
        const room = rooms.get(socket.roomCode);
        const peer = socket.role === "host" ? room?.guest : room?.host;
        if (peer && !peer.destroyed) {
          send(peer, line);
        }
      }

      newline = buffer.indexOf("\n");
    }
  });

  socket.on("close", () => cleanup(socket));
  socket.on("error", () => cleanup(socket));
});

server.listen(port, "0.0.0.0", () => {
  console.log(`Ranch relay listening on TCP ${port}`);
});
