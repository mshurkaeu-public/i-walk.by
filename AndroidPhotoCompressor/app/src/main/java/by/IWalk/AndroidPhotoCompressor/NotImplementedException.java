package by.IWalk.AndroidPhotoCompressor;

// Мой аналаг для .Net System.NotImplementedException
// Здаецца, у java свайга такого аналага няма.
// У маім кодзе ёсць месцы, якія я не жадаю апрацоўваць, а жадаю каб праграма проста ўпала.
public class NotImplementedException extends RuntimeException {
    public NotImplementedException() {
        super();
    }

    public NotImplementedException(String message) {
        super(message);
    }

    public NotImplementedException(Throwable cause) {
        super(cause);
    }
}
