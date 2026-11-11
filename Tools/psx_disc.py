"""Read ISO9660 files and data payloads from a raw 2352-byte-sector PSX image."""
from pathlib import Path
import struct


class DiscImage:
    def __init__(self, path):
        self.path = Path(path)

    def read_data(self, lba, sectors):
        chunks = []
        with self.path.open('rb') as handle:
            handle.seek(lba * 2352)
            for index in range(sectors):
                raw = handle.read(2352)
                if len(raw) != 2352 or raw[:12] != b'\0' + b'\xff' * 10 + b'\0':
                    raise ValueError(f'Invalid raw data sector {lba + index}')
                if raw[15] == 1:
                    chunks.append(raw[16:2064])
                elif raw[15] == 2 and raw[16:20] == raw[20:24] and not raw[18] & 0x20:
                    chunks.append(raw[24:2072])
                else:
                    raise ValueError(f'Sector {lba + index} is not a supported Form-1 data sector')
        return b''.join(chunks)

    @staticmethod
    def _record(data, offset):
        length = data[offset]
        if length < 34 or offset + length > len(data):
            raise ValueError('Invalid ISO directory record')
        extent, size = struct.unpack_from('<I', data, offset + 2)[0], struct.unpack_from('<I', data, offset + 10)[0]
        if extent != struct.unpack_from('>I', data, offset + 6)[0] or size != struct.unpack_from('>I', data, offset + 14)[0]:
            raise ValueError('ISO directory endian copies differ')
        count = data[offset + 32]
        name = data[offset + 33:offset + 33 + count]
        return dict(lba=extent, bytes=size, directory=bool(data[offset + 25] & 2),
                    name=name.decode('ascii').split(';')[0], record_bytes=length)

    def locate(self, path):
        pvd = self.read_data(16, 1)
        if pvd[0] != 1 or pvd[1:6] != b'CD001':
            raise ValueError('No ISO9660 primary volume descriptor')
        current = self._record(pvd, 156)
        for component in path.replace('\\', '/').split('/'):
            if not current['directory']:
                raise FileNotFoundError(path)
            data = self.read_data(current['lba'], (current['bytes'] + 2047) // 2048)[:current['bytes']]
            offset = 0
            found = None
            while offset < len(data):
                if data[offset] == 0:
                    offset = ((offset // 2048) + 1) * 2048
                    continue
                record = self._record(data, offset)
                if record['name'].upper() == component.upper():
                    found = record
                    break
                offset += record['record_bytes']
            if found is None:
                raise FileNotFoundError(path)
            current = found
        return current

    def read_file(self, path):
        record = self.locate(path)
        if record['directory']:
            raise IsADirectoryError(path)
        data = self.read_data(record['lba'], (record['bytes'] + 2047) // 2048)[:record['bytes']]
        return data, record
