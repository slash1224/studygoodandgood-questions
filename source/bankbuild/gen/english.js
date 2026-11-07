const { pairs, facts } = require('./core');

// 单词 → 中文（按主题分组，干扰项自动取自同组其它词义）
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['dog', '狗'], ['cat', '猫'], ['bird', '鸟'], ['fish', '鱼'], ['horse', '马'],
  ['rabbit', '兔子'], ['tiger', '老虎'], ['lion', '狮子'], ['elephant', '大象'], ['monkey', '猴子'],
  ['panda', '熊猫'], ['bear', '熊'], ['snake', '蛇'], ['sheep', '绵羊'], ['cow', '奶牛'],
  ['duck', '鸭子'], ['chicken', '鸡'], ['mouse', '老鼠'], ['wolf', '狼'], ['fox', '狐狸'],
  ['giraffe', '长颈鹿'], ['kangaroo', '袋鼠'], ['penguin', '企鹅'], ['dolphin', '海豚'], ['whale', '鲸'],
  ['frog', '青蛙'], ['ant', '蚂蚁'], ['bee', '蜜蜂'], ['butterfly', '蝴蝶'], ['spider', '蜘蛛']
]);
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['apple', '苹果'], ['banana', '香蕉'], ['orange', '橘子'], ['pear', '梨'], ['grape', '葡萄'],
  ['watermelon', '西瓜'], ['strawberry', '草莓'], ['tomato', '西红柿'], ['potato', '土豆'], ['carrot', '胡萝卜'],
  ['onion', '洋葱'], ['bread', '面包'], ['cake', '蛋糕'], ['rice', '米饭'], ['noodle', '面条'],
  ['egg', '鸡蛋'], ['milk', '牛奶'], ['water', '水'], ['juice', '果汁'], ['coffee', '咖啡'],
  ['tea', '茶'], ['meat', '肉'], ['beef', '牛肉'], ['pork', '猪肉'], ['soup', '汤'],
  ['sugar', '糖'], ['salt', '盐'], ['chocolate', '巧克力'], ['candy', '糖果'], ['ice cream', '冰淇淋']
]);
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['school', '学校'], ['teacher', '老师'], ['student', '学生'], ['classroom', '教室'], ['library', '图书馆'],
  ['book', '书'], ['pen', '钢笔'], ['pencil', '铅笔'], ['ruler', '尺子'], ['eraser', '橡皮'],
  ['backpack', '书包'], ['homework', '家庭作业'], ['exam', '考试'], ['lesson', '课'], ['blackboard', '黑板'],
  ['desk', '课桌'], ['chair', '椅子'], ['computer', '电脑'], ['dictionary', '词典'], ['notebook', '笔记本'],
  ['science', '科学'], ['history', '历史'], ['math', '数学'], ['English', '英语'], ['music', '音乐'],
  ['art', '美术'], ['geography', '地理'], ['biology', '生物'], ['chemistry', '化学'], ['physics', '物理']
]);
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['run', '跑'], ['jump', '跳'], ['swim', '游泳'], ['sing', '唱歌'], ['dance', '跳舞'],
  ['read', '阅读'], ['write', '写'], ['draw', '画画'], ['listen', '听'], ['speak', '说'],
  ['walk', '走'], ['play', '玩'], ['eat', '吃'], ['drink', '喝'], ['sleep', '睡觉'],
  ['study', '学习'], ['buy', '买'], ['sell', '卖'], ['open', '打开'], ['close', '关闭'],
  ['help', '帮助'], ['find', '找到'], ['lose', '丢失'], ['give', '给'], ['take', '拿'],
  ['make', '制作'], ['bring', '带来'], ['carry', '搬运'], ['watch', '观看'], ['come', '来']
]);
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['big', '大的'], ['small', '小的'], ['long', '长的'], ['short', '短的'], ['tall', '高的'],
  ['low', '低的'], ['old', '老的'], ['young', '年轻的'], ['new', '新的'], ['hot', '热的'],
  ['cold', '冷的'], ['warm', '温暖的'], ['cool', '凉爽的'], ['fast', '快的'], ['slow', '慢的'],
  ['happy', '高兴的'], ['sad', '伤心的'], ['angry', '生气的'], ['tired', '疲倦的'], ['busy', '忙碌的'],
  ['free', '空闲的'], ['easy', '容易的'], ['difficult', '困难的'], ['important', '重要的'], ['beautiful', '美丽的'],
  ['clean', '干净的'], ['dirty', '脏的'], ['strong', '强壮的'], ['weak', '虚弱的'], ['rich', '富有的']
]);
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['sun', '太阳'], ['moon', '月亮'], ['star', '星星'], ['sky', '天空'], ['cloud', '云'],
  ['rain', '雨'], ['snow', '雪'], ['wind', '风'], ['storm', '暴风雨'], ['river', '河流'],
  ['lake', '湖泊'], ['sea', '海洋'], ['mountain', '山'], ['hill', '小山'], ['forest', '森林'],
  ['tree', '树'], ['flower', '花'], ['grass', '草'], ['leaf', '叶子'], ['stone', '石头'],
  ['sand', '沙子'], ['island', '岛屿'], ['beach', '海滩'], ['spring', '春天'], ['summer', '夏天'],
  ['autumn', '秋天'], ['winter', '冬天'], ['weather', '天气'], ['temperature', '温度'], ['rainbow', '彩虹']
]);
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['city', '城市'], ['village', '村庄'], ['country', '国家'], ['road', '道路'], ['street', '街道'],
  ['bridge', '桥'], ['park', '公园'], ['hospital', '医院'], ['bank', '银行'], ['hotel', '旅馆'],
  ['restaurant', '餐馆'], ['supermarket', '超市'], ['station', '车站'], ['airport', '机场'], ['bus', '公共汽车'],
  ['train', '火车'], ['car', '小汽车'], ['bike', '自行车'], ['plane', '飞机'], ['ship', '轮船'],
  ['taxi', '出租车'], ['subway', '地铁'], ['ticket', '票'], ['map', '地图'], ['museum', '博物馆'],
  ['zoo', '动物园'], ['cinema', '电影院'], ['factory', '工厂'], ['farm', '农场'], ['library', '图书馆']
]);
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['father', '父亲'], ['mother', '母亲'], ['brother', '兄弟'], ['sister', '姐妹'], ['grandfather', '祖父'],
  ['grandmother', '祖母'], ['uncle', '叔叔'], ['aunt', '阿姨'], ['cousin', '堂表兄弟姐妹'], ['son', '儿子'],
  ['daughter', '女儿'], ['baby', '婴儿'], ['child', '孩子'], ['friend', '朋友'], ['doctor', '医生'],
  ['nurse', '护士'], ['driver', '司机'], ['farmer', '农民'], ['worker', '工人'], ['police', '警察'],
  ['soldier', '士兵'], ['singer', '歌手'], ['actor', '演员'], ['artist', '艺术家'], ['writer', '作家'],
  ['scientist', '科学家'], ['engineer', '工程师'], ['cook', '厨师'], ['waiter', '服务员'], ['king', '国王']
]);
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['head', '头'], ['hair', '头发'], ['face', '脸'], ['eye', '眼睛'], ['ear', '耳朵'],
  ['nose', '鼻子'], ['mouth', '嘴'], ['tooth', '牙齿'], ['neck', '脖子'], ['arm', '手臂'],
  ['hand', '手'], ['finger', '手指'], ['leg', '腿'], ['foot', '脚'], ['knee', '膝盖'],
  ['back', '背'], ['heart', '心脏'], ['stomach', '胃'], ['shoulder', '肩膀'], ['body', '身体']
]);
pairs('english', 0, 1, '单词 "{}" 的中文意思是？', [
  ['table', '桌子'], ['bed', '床'], ['door', '门'], ['window', '窗户'], ['wall', '墙'],
  ['floor', '地板'], ['light', '灯'], ['phone', '电话'], ['television', '电视'], ['clock', '时钟'],
  ['key', '钥匙'], ['bag', '包'], ['box', '盒子'], ['cup', '杯子'], ['bowl', '碗'],
  ['plate', '盘子'], ['knife', '刀'], ['fork', '叉子'], ['spoon', '勺子'], ['towel', '毛巾']
]);

// 短语
pairs('english', 0, 2, '短语 "{}" 的意思是？', [
  ['look after', '照顾'], ['look for', '寻找'], ['look up', '查阅'], ['give up', '放弃'], ['take off', '起飞'],
  ['put on', '穿上'], ['turn on', '打开'], ['turn off', '关闭'], ['get up', '起床'], ['go to bed', '上床睡觉'],
  ['wake up', '醒来'], ['wait for', '等待'], ['look at', '看'], ['come back', '回来'], ['go out', '出去'],
  ['find out', '查明'], ['carry out', '执行'], ['set up', '建立'], ['pick up', '捡起'], ['put away', '收起'],
  ['run out of', '用完'], ['be good at', '擅长'], ['be interested in', '对……感兴趣'], ['be afraid of', '害怕'], ['be proud of', '为……骄傲'],
  ['depend on', '依靠'], ['belong to', '属于'], ['agree with', '同意'], ['arrive at', '到达'], ['laugh at', '嘲笑'],
  ['worry about', '担心'], ['hear from', '收到……的来信'], ['ask for', '请求'], ['pay for', '付款'], ['look forward to', '期待'],
  ['make sure', '确保'], ['take part in', '参加'], ['instead of', '代替'], ['because of', '因为'], ['as soon as', '一……就'],
  ['as well as', '也'], ['such as', '例如'], ['at least', '至少'], ['at first', '起初'], ['in the end', '最后'],
  ['on time', '准时'], ['in time', '及时'], ['by accident', '偶然'], ['in fact', '事实上'], ['right away', '立刻'],
  ['over and over', '反复'], ['more and more', '越来越'], ['neither...nor', '既不……也不'], ['either...or', '要么……要么'], ['both...and', '两者都']
]);

// 语法填空
facts('english', 0, 2, [
  ['He ______ to school by bus every day.', 'goes', ['go', 'going', 'went']],
  ['They ______ football now.', 'are playing', ['play', 'played', 'plays']],
  ['I ______ my homework yesterday.', 'did', ['do', 'does', 'doing']],
  ['She ______ to Beijing next week.', 'will go', ['goes', 'went', 'going']],
  ['There ______ some milk in the glass.', 'is', ['are', 'be', 'were']],
  ['This book is ______ than that one.', 'more interesting', ['interesting', 'most interesting', 'interested']],
  ['He is the ______ student in our class.', 'tallest', ['taller', 'tall', 'most tall']],
  ['My sister ______ TV when I got home.', 'was watching', ['watches', 'watch', 'is watching']],
  ['The window ______ by Tom last night.', 'was broken', ['broke', 'is broken', 'breaks']],
  ['Would you like ______ some tea?', 'to drink', ['drink', 'drinking', 'drank']],
  ['I enjoy ______ music after school.', 'listening to', ['listen to', 'to listen', 'listened']],
  ['We have ______ apples than they do.', 'fewer', ['few', 'less', 'least']],
  ['______ you ever been to Shanghai?', 'Have', ['Do', 'Did', 'Are']],
  ['If it ______ tomorrow, we will stay at home.', 'rains', ['rain', 'rained', 'will rain']],
  ['He asked me ______ I could help him.', 'if', ['that', 'what', 'which']],
  ['Neither Tom nor his friends ______ the answer.', 'know', ['knows', 'knowing', 'known']],
  ['The teacher told us ______ late again.', 'not to be', ['don\'t be', 'not be', 'to not be']],
  ['It is important ______ English well.', 'to learn', ['learn', 'learning', 'learned']],
  ['She has lived here ______ 2010.', 'since', ['for', 'in', 'at']],
  ['There are ______ months in a year.', 'twelve', ['twelfth', 'twelveth', 'the twelve']],
  ['Look! The children ______ in the park.', 'are singing', ['sing', 'sings', 'sang']],
  ['I don\'t know ______ to say.', 'what', ['how', 'which', 'that']],
  ['This is the boy ______ helped me.', 'who', ['which', 'whom', 'whose']],
  ['He is used to ______ early.', 'getting up', ['get up', 'got up', 'gets up']],
  ['The meeting ______ next Friday.', 'will be held', ['will hold', 'is holding', 'held']],
  ['______ hard, and you will succeed.', 'Work', ['Working', 'To work', 'Worked']],
  ['She is ______ honest girl.', 'an', ['a', 'the', '/']],
  ['We should protect ______ environment.', 'the', ['a', 'an', '/']],
  ['I have ______ useful book.', 'a', ['an', 'the', '/']],
  ['Both of ______ are from China.', 'them', ['they', 'their', 'theirs']],
  ['This is ______ book. Mine is over there.', 'his', ['he', 'him', 'himself']],
  ['The girl ______ a red dress is my sister.', 'in', ['on', 'at', 'with']],
  ['He was born ______ May 5th.', 'on', ['in', 'at', 'for']],
  ['We will finish the work ______ two hours.', 'in', ['after', 'for', 'since']],
  ['She spoke ______ loudly that everyone could hear.', 'so', ['such', 'too', 'very']],
  ['It is ______ a nice day that we want to go out.', 'such', ['so', 'too', 'very']],
  ['He runs ______ than his brother.', 'faster', ['fast', 'fastest', 'more fast']],
  ['The more you read, ______ you will be.', 'the wiser', ['wiser', 'the wise', 'wisest']],
  ['I would rather ______ at home.', 'stay', ['to stay', 'staying', 'stayed']],
  ['She made him ______ the truth.', 'tell', ['to tell', 'telling', 'told']],
  ['The room needs ______.', 'cleaning', ['clean', 'to clean it', 'cleaned by']],
  ['He suggested ______ a meeting.', 'holding', ['hold', 'to hold', 'held']],
  ['It\'s no use ______ over spilled milk.', 'crying', ['cry', 'to cry', 'cried']],
  ['I\'m looking forward to ______ from you.', 'hearing', ['hear', 'heard', 'be heard']],
  ['By the time he arrived, the film ______.', 'had begun', ['began', 'has begun', 'begins']],
  ['Not only he but also I ______ interested in it.', 'am', ['is', 'are', 'be']],
  ['The number of students ______ growing.', 'is', ['are', 'were', 'be']],
  ['A number of students ______ playing outside.', 'are', ['is', 'was', 'be']],
  ['He hardly goes out, ______ he?', 'does', ['doesn\'t', 'is', 'isn\'t']]
]);

module.exports = {};
